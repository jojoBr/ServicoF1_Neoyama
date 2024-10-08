using SAPbobsCOM;
using ServicoF1.Models.F1.Matriz_Tributaria;
using ServicoF1.Models.F1.MatrizIPI;
using ServicoF1.Models.F1.Regime_Tributario;
using ServicoF1.Models.WEB;
using ServicoF1.Uteis;
using System;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ServicoF1.servicos
{
    public sealed class TributeService
    {
        private const string TAX_REGIME_URL = "https://apidatain-prod.f1ws.com.br/api/tax_regime";
        private const string TAX_MATRIX_URL = "https://apidatain-prod.f1ws.com.br/api/tax_matrix";
        private const string TAX_MATRIX_IPI_URL = "https://apidatain-prod.f1ws.com.br/api/tax_matrix_ipi";
        private readonly ILogger<Worker> _logger;
        private readonly API.ApiConnection _connection;
        private readonly string _token;
        private readonly bool _homolog;

        public TributeService(ILogger<Worker> logger, string token, bool homolog = false)
        {
            _logger = logger;
            _homolog = homolog;
            _token = token;
            _connection = new API.ApiConnection(_token, _logger);
        }

        public void Run(bool DataSendIntoLogger)
        {
            CadastrarRegimesTributarios();
            // TODO: preciso preparar uma maneira distinguir pq somente uma unica vez isso deve ser feito
            // e caso seja necessário o add eu preciso eliminar os que já estão cadastrados em caso de erro pq na pesquisa talvez seja improvável
            // talvez com uma tabela de usuário para isto
            // 11/11/2022- Catiele da F1 confirmou que o post faz a adição e o update dos dados ao mesmo tempo então só é necessário um endpoint
            // reunião em dezembro com o jobson confirmou que a matriz de api não é necessária para a neoyama
            // matriz de API voltou a ser necessária
            CadastraDadosTibutarios(DataSendIntoLogger);
        }

        private void CadastraDadosTibutarios(bool DataSendIntoLogger)
        {
            try
            {
                Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string query = "SELECT U_Cd_Venda_SAP, U_Cd_Revenda_SAP, Code FROM \"@F1_UTILIZACAO\" WHERE U_status = 'A' AND ISNULL(U_CadastrarF1, 'N') = 'Y'";
                recordset.DoQuery(query);
                while (!recordset.EoF) 
                {
                    int usage = (int)recordset.Fields.Item(0).Value;
                    int usage2 = (int)recordset.Fields.Item(1).Value;
                    int sell_type = Convert.ToInt32((string)recordset.Fields.Item(2).Value);
                    if (sell_type == 0)
                    {
                        recordset.MoveNext();
                        continue;
                    }
                    _logger.LogInformation("Cadastrando tipo de venda {type}", sell_type);
                    CadastraMatrizesIPI(DataSendIntoLogger, usage, usage2, sell_type);
                    GenerateTributeMatrix(DataSendIntoLogger, usage, usage2, sell_type);

                    recordset.MoveNext();
                }
            }
            catch(Exception ex) 
            {
                _logger.LogError("houve um erro ao tentar pesquisar os dados de ipi e icms por utilização. Erro: {erro}", ex.Message);
            }
        }

        public void CadastraMatrizesIPI(bool DataSendIntoLogger, int usage, int usage2, int sell_type)
        {
            try
            {
                AdicionaMatrizeIPI(DataSendIntoLogger, usage.ToString(), sell_type);
                AdicionaMatrizeIPI(DataSendIntoLogger, usage2.ToString(), sell_type);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Erro ao tentar cadastrar as matrizes de IPI: {error}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        private async Task AdicionarMatrizIPI(bool DataSendIntoLogger, string usage, int sell_type, Matrizes_Tributaria_IPI matrizes)
        {
            Header[] header = new Header[1] { new Header("Bearer", _token) };
            string url = TAX_MATRIX_IPI_URL;
            if (_homolog)
                url = url.Replace("apidatain-prod", "apidatain-homolog");

            _logger.LogInformation("Cadastrando {qtd} matrizes para o ipi com utilização {usage} e tipo de venda {type}", matrizes.data.Count, usage, sell_type);
            if (matrizes.data.Count > 0)
            {
                if (DataSendIntoLogger)
                    _logger.LogInformation("Cadastrando dados de IPI [{data}]", JsonSerializer.Serialize(matrizes));
                int qtdEnviada = 0;
                int offset = 400;
                while (qtdEnviada < matrizes.data.Count)
                {
                    if (qtdEnviada + 400 > matrizes.data.Count)
                        offset = matrizes.data.Count - qtdEnviada;
                    _logger.LogInformation("Enviando {qtd} valores de ipi  da matriz", offset);
                    Matrizes_Tributaria_IPI cutDownPortion = new Matrizes_Tributaria_IPI(matrizes.data.GetRange(qtdEnviada, offset));
                    _ = await _connection.POST<Matrizes_Tributaria_IPI, Matriz_Tributaria_IPI[]>(url, header, cutDownPortion);
                    _logger.LogInformation("{val} regras enviadas", cutDownPortion.data.Count);
                    qtdEnviada += offset;
                }
            }
        }

        private void AdicionaMatrizeIPI(bool DataSendIntoLogger, string usage, int sell_type)
        {
            try
            {
                Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                Recordset recordsetDeterminacao = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                // busca id das determinações usadas para o IPI
                recordsetDeterminacao.DoQuery(Queries.Determinacoes_IPI);
                while (!recordsetDeterminacao.EoF)
                {
                    try
                    {
                        string regra = Convert.ToString(recordsetDeterminacao.Fields.Item(0).Value);
                        _logger.LogInformation("Buscando Valores para o uso {use} e venda {sell} e regra {rule}", usage, sell_type, regra);
                        Matrizes_Tributaria_IPI matrizes_Tributaria_IPI = new Matrizes_Tributaria_IPI();
                        string query = Queries.Impostos_IPI.Replace("@usage", usage).Replace("@idRegra", regra);
                        ExpandQuery(ref query, recordsetDeterminacao);

                        if (DataSendIntoLogger)
                        {
                            _logger.LogInformation("----------------------------");
                            _logger.LogInformation("query de ipi para uso {uso} e tipo {regra} \n {query}", usage, regra, query);
                            _logger.LogInformation("----------------------------");
                        }

                        recordset.DoQuery(query);
                        _logger.LogInformation("{val} regras encontradas", recordset.RecordCount);
                        while (!recordset.EoF)
                        {
                            Matriz_Tributaria_IPI _IPI = new Matriz_Tributaria_IPI();
                            SetKeyValue(ref _IPI, recordset.Fields.Item("chave1").Value, recordset.Fields.Item("CampoChave1").Value, recordset);
                            SetKeyValue(ref _IPI, recordset.Fields.Item("chave2").Value, recordset.Fields.Item("CampoChave2").Value, recordset);
                            SetKeyValue(ref _IPI, recordset.Fields.Item("chave3").Value, recordset.Fields.Item("CampoChave3").Value, recordset);
                            SetKeyValue(ref _IPI, recordset.Fields.Item("chave4").Value, recordset.Fields.Item("CampoChave4").Value, recordset);
                            SetKeyValue(ref _IPI, recordset.Fields.Item("chave5").Value, recordset.Fields.Item("CampoChave5").Value, recordset);
                            _IPI.ipi = Convert.ToString(recordset.Fields.Item("ipi").Value);
                            _IPI.ipi = _IPI.ipi.Replace(",", ".");
                            _IPI.external_order_type = sell_type.ToString();
                            _IPI.code = $"{_IPI.ncm}_{_IPI.tax_regime}_{_IPI.destination_state}_{_IPI.product_origin}_{_IPI.item_code}_{removeCnpjMask(_IPI.client_cnpj)}_{sell_type}_{usage}";
                            matrizes_Tributaria_IPI.data.Add(_IPI);
                            recordset.MoveNext();
                        }

                        AdicionarMatrizIPI(DataSendIntoLogger, usage, sell_type, matrizes_Tributaria_IPI).Wait();
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError("Erro ao tentar buscar os dados de ipi para utilização {usage} tipo de venda {sell} e regra {rule}. Erro: {erro}", usage, sell_type, (int)recordsetDeterminacao.Fields.Item(0).Value, ex.Message);
                    }

                    recordsetDeterminacao.MoveNext();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar buscar os dados de ipi para utilização {uage}. Erro: {erro}", usage, ex.Message);
            }
        }

        private static string removeCnpjMask(string cnpj)
        {
            return cnpj.Replace("/", "").Replace(".", "").Replace("-", "");
        }

        private void ExpandQuery(ref string query, Recordset recordsetDeterminacao)
        {
            int id = recordsetDeterminacao.Fields.Item(0).Value;
            string whereClause = "";
            bool hasOrigem = false;
            query += GetJoin(Convert.ToString(recordsetDeterminacao.Fields.Item(1).Value), 1, ref whereClause, ref hasOrigem);
            query += GetJoin(Convert.ToString(recordsetDeterminacao.Fields.Item(2).Value), 2, ref whereClause, ref hasOrigem);
            query += GetJoin(Convert.ToString(recordsetDeterminacao.Fields.Item(3).Value), 3, ref whereClause, ref hasOrigem);
            query += GetJoin(Convert.ToString(recordsetDeterminacao.Fields.Item(4).Value), 4, ref whereClause, ref hasOrigem);
            query += GetJoin(Convert.ToString(recordsetDeterminacao.Fields.Item(5).Value), 5, ref whereClause, ref hasOrigem);
            query += @$" {whereClause}
                    order by x.row_num
                    drop table #tab";
        }

        private static string GetJoin(string value, int keyPosition, ref string whereClause, ref bool hasOrigem)
        {
            if (value == "0")
                return "";

            switch (value)
            {
                // ItemCode
                case "2":
                    {
                        if (string.IsNullOrEmpty(whereClause))
                            whereClause += $"WHERE CampoChave{keyPosition} in (SELECT ItemCode FROM OITM WHERE U_CadastrarF1 = 'Y')";
                        else
                            whereClause += $"  and CampoChave{keyPosition} in (SELECT ItemCode FROM OITM WHERE U_CadastrarF1 = 'Y')";
                        return "";
                    }
                    // PN
                case "1":
                    {
                        if (string.IsNullOrEmpty(whereClause))
                            whereClause += $"WHERE CampoChave{keyPosition} in (SELECT TaxId0 FROM CRD7 INNER JOIN OCRD ON CRD7.CardCode = OCRD.CardCode WHERE OCRD.U_CadastraF1 = 'Y')";
                        else
                            whereClause += $" and CampoChave{keyPosition} in (SELECT TaxId0 FROM CRD7 INNER JOIN OCRD ON CRD7.CardCode = OCRD.CardCode WHERE OCRD.U_CadastraF1 = 'Y')";
                        return "";
                    }
                case "9":
                    {
                        //Grupo de Item
                        return $" INNER JOIN (SELECT ItemCode, ItmsGrpNam, U_CadastrarF1 FROM OITM INNER JOIN oitb ON OITM.ItmsGrpCod = oitb.ItmsGrpCod) OITM on x.CampoChave{keyPosition} = OITM.ItmsGrpNam and OITM.U_CadastrarF1 = 'Y'";
                    }
                case "11":
                    {
                        //Grupo de clientes
                        return $" LEFT JOIN(Select CRD7.*, OCRG.GroupName, OCRD.U_CadastraF1 FROM CRD7 LEFT JOIN OCRD ON CRD7.CardCode = OCRD.CardCode LEFT JOIN OCRG on OCRG.GroupCode  = OCRD.GroupCode) T ON T.GroupName = x.CampoChave{keyPosition} AND T.U_CadastraF1 = 'Y'";
                    }
                case "26":
                    {
                        //tipo tributario
                        return $" LEFT JOIN (SELECT Code as 'Tipo_trib', Descr FROM OBNI WHERE OBNI.IndexType = 18) trib on x.CampoChave{keyPosition} = trib.Descr";
                    }
                case "27":
                    {
                        //Grupo de Estado
                        return $" LEFT JOIN (SELECT Code as 'estado', OSTG.GroupName FROM OCST INNER JOIN OSTG ON OCST.GroupCode = OSTG.GroupCode) Estados on x.CampoChave{keyPosition} = Estados.GroupName";
                    }
                case "28":
                    {
                        //Grupo de origem
                        hasOrigem = true;
                        return $" LEFT JOIN (SELECT Code as 'origem', OPSG.GroupName FROM OPSC INNER JOIN OPSG on OPSC.GroupCode = OPSG.GroupCode) ORGRP on x.CampoChave{keyPosition} = ORGRP.GroupName";
                    }
                case "29":
                    {
                        // Grupo de NCM
                        return $" LEFT JOIN (SELECT ONCM.NcmCode, ONCG.GroupName FROM ONCM  INNER JOIN ONCG ON ONCG.GroupCode = ONCM.\"Group\") NCM on x.CampoChave{keyPosition} = NCM.GroupName";
                    }
                case "17":
                    {
                        // TIPO de materiais
                        // TODO: Adicionar Controle quando houver origem
                        string origemControl = "";
                        if (hasOrigem)
                            origemControl = "and items.ProductSrc = ORGRP.origem";
                        return $" INNER JOIN (SELECT ItemCode, OMTP.AbsEntry, ProductSrc, U_CadastrarF1 FROM OITM INNER JOIN OMTP ON OITM.MatType = OMTP.AbsEntry) items on x.CampoChave{keyPosition}  = items.AbsEntry and U_CadastrarF1 = 'Y' {origemControl}";
                    }
                default:
                    return "";
            }
        }

        private static void SetKeyValue(ref Matriz_Tributaria_IPI iPI, string chave, string value, Recordset recordset)
        {
            if(string.IsNullOrEmpty(chave))
                return;
            // adicionar os valores para grupos de item grupo de grupo de clientes, tipo tributário, grupo de estado, grupo de origem, grupo de ncm e usar o CRD7 para o PN
            switch (chave)
            {
                case "ITEM":
                    {
                        iPI.item_code = value;
                    }
                    break;
                case "UF":
                    {
                        iPI.destination_state = value;
                    }
                    break;
                case "TIPO_TRIBUTARIO_PN":
                    {
                        iPI.tax_regime = Convert.ToString(recordset.Fields.Item("Tipo_trib").Value);
                    }
                    break;
                case "NCM":
                    {
                        iPI.ncm = value;
                    }
                    break;
                case "GRUPO_DE_CODIGO_DE_ORIGEM":
                    {
                        iPI.product_origin = Convert.ToString(recordset.Fields.Item("origem").Value);
                    }
                    break;
                case "PN":
                    {
                        iPI.client_cnpj = value;//recordset.Fields.Item("TaxId0").Value;
                    }
                    break;
                case "GRUPO_DE_ITEM":
                    {
                        iPI.item_code = Convert.ToString(recordset.Fields.Item("ItemCode").Value);
                    }
                    break;
                case "GRUPO_DE_CLIENTES":
                    {
                        iPI.client_cnpj = Convert.ToString(recordset.Fields.Item("TaxId0").Value);
                    }
                    break;
                case "GRUPO_DE_ESTADO":
                    {
                        iPI.destination_state = Convert.ToString(recordset.Fields.Item("estado").Value);
                    }
                    break;
                case "GRUPO_DE_CODIGO_NCM":
                    {
                        iPI.ncm = Convert.ToString(recordset.Fields.Item("NcmCode").Value);
                    }
                    break;
                case "TIPO_DE_MATERIAL":
                    {
                        iPI.item_code = Convert.ToString(recordset.Fields.Item("ItemCode").Value);
                    }
                    break;

            }
        }

        private void GenerateTributeMatrix(bool DataSendIntoLogger, int usage, int usage2, int sell_type)
        {
            try
            {
                int count = 0;
                // utilizacao de venda
                Matrizes matrizes = GetTributeMatrix(usage, sell_type);
                count += matrizes.data.Length;
                CadastraMatrix(matrizes, usage, DataSendIntoLogger);
                // utilização de revenda
                matrizes = GetTributeMatrix(usage2, sell_type);
                count += matrizes.data.Length;
                CadastraMatrix(matrizes, usage, DataSendIntoLogger);

                _logger.LogInformation("Quantidade total cadastrada: {count}", count);
            }
            catch(Exception ex) 
            {
                _logger.LogError("Erro ao tentar cadastrar a matriz tributária: {error}", ex.Message);
            }
        }

        /// <summary>
        /// gera o código para a matriz tributária
        /// </summary>
        /// <param name="matriz"> Matriz a qual o código deve ser criado </param>
        /// <param name="usage"> a utilização dentro do SAP ligada a matriz </param>
        /// <returns> o novo código </returns>
        private static string GeneratedCode(Matriz matriz, int usage, int sell_type)
        {
            // caso não tenha um cnpj o caractér * é utilizado indicando na plataforma que a regra é independente de cnpj
            string cnpj = string.IsNullOrEmpty(matriz.client_cnpj) ? "*" : removeCnpjMask(matriz.client_cnpj);
            // codigo gerado com o padrão de ncmCode|cnpj|regime_tributario|tipo_icms|estado_da_mercadoria|estado_destino|item|utilização
            string code = $"{matriz.ncm}|{cnpj}|{matriz.tax_regime}|{matriz.icms_type}|{matriz.home_state}|{matriz.destination_state}|{matriz.item_code}|{matriz.city_destination}|" + 
            $"{matriz.product_origin}|{matriz.intern_icms}|{matriz.extern_icms}|{sell_type}|{usage}";
            return code;
        }

        private async void CadastraMatrix(Matrizes matrizes, int usage, bool DataSendIntoLogger)
        {
            try
            {
                if (matrizes.data.Length < 1)
                    return;

                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = TAX_MATRIX_URL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");
                /// criar um threashold para o envio dos dados utilizar 300 de inicio
                if (matrizes.data.Length < 301)
                {
                    _logger.LogInformation("Cadastrando matrizes para o uso {usage}", usage);
                    if (DataSendIntoLogger)
                        _logger.LogInformation("Data Sent was [{data}]", JsonSerializer.Serialize(matrizes));

                    _ = await _connection.POST<Matrizes, Matriz[]>(url, header, matrizes);
                }
                else
                {
                    double division = matrizes.data.Length / 300;
                    int total_sent = 0;
                    for (int index = 0; index < division; index++)
                    {
                        Matrizes sub_matriz = new Matrizes(300);
                        sub_matriz.data = matrizes.data.ToList().GetRange(total_sent, 300).ToArray();
                        if (DataSendIntoLogger)
                            _logger.LogInformation("Data Sent was [{data}]", JsonSerializer.Serialize(sub_matriz));

                        _ = await _connection.POST<Matrizes, Matriz[]>(url, header, sub_matriz);
                        total_sent += 300;
                        _logger.LogInformation("Cadastrando parcialmente {qtd} matrizes para o uso {usage}", total_sent, usage);
                    }

                    if (total_sent < matrizes.data.Length)
                    {
                        int QtdRestandte = matrizes.data.Length - total_sent;
                        Matrizes sub_matriz = new Matrizes(300)
                        {
                            data = matrizes.data.ToList().GetRange(total_sent, QtdRestandte).ToArray()
                        };

                        _logger.LogInformation("Cadastrando parcialmente {qtd} matrizes para o uso {usage}", matrizes.data.Length, usage);
                        if (DataSendIntoLogger)
                            _logger.LogInformation("Data Sent was [{data}]", JsonSerializer.Serialize(sub_matriz));

                        _ = await _connection.POST<Matrizes, Matriz[]>(url, header, sub_matriz);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar cadastrar a matriz tributária para o uso {usage}: {erro}", usage, ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        private Matrizes GetTributeMatrix(int usage, int sell_type)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            try
            {
                string query = Queries.Busca_tributos_por_uso.Replace("@usage", usage.ToString());
                recordset.DoQuery(query);
                Matrizes matrizes = new (recordset.RecordCount);
                int count = 0;
                while (!recordset.EoF)
                {
                    matrizes.data[count] = new Matriz()
                    {
                        ncm = ((string)recordset.Fields.Item(1).Value).Replace(".", ""),
                        tax_regime = Convert.ToString(recordset.Fields.Item(2).Value),
                        home_state = Convert.ToString(recordset.Fields.Item(3).Value),
                        destination_state = Convert.ToString(recordset.Fields.Item(4).Value),
                        icms_type = Convert.ToString(recordset.Fields.Item(5).Value),
                        intern_icms = (float)Convert.ToDouble(recordset.Fields.Item(6).Value),
                        extern_icms = (float)Convert.ToDouble(recordset.Fields.Item(7).Value),
                        product_origin = Convert.ToString(recordset.Fields.Item(8).Value),
                        st = (float)Convert.ToDouble(recordset.Fields.Item(9).Value),
                        item_code = Convert.ToString(recordset.Fields.Item(10).Value),
                        client_cnpj = Convert.ToString(recordset.Fields.Item(11).Value),
                        icms_internal_destination = (float)Convert.ToDouble(recordset.Fields.Item(12).Value),
                        icms_reduction = Convert.ToInt32(recordset.Fields.Item(13).Value),
                        fcp_st = Convert.ToInt32(recordset.Fields.Item(14).Value),
                        fcp_base = Convert.ToInt32(recordset.Fields.Item(15).Value),
                        sell_type = Convert.ToString(recordset.Fields.Item(16).Value),
                        nature_operation = Convert.ToString(recordset.Fields.Item(17).Value),
                        city_destination = Convert.ToString(recordset.Fields.Item(18).Value),
                    };
                    matrizes.data[count].code = GeneratedCode(matrizes.data[count], usage, sell_type);
                    matrizes.data[count].external_order_type = sell_type.ToString(); // código para um campo que deverá aparecer no pedido de vendas

                    recordset.MoveNext();
                    count++;
                }

                _logger.LogInformation("Pesquisa de {qtd} registros para o cadastro da matriz relacionada a utilização {usage}", matrizes.data.Length, usage);
                return matrizes;
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar cadastrar as matrizes para o uso : {usage} | {erro}", usage, ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return new Matrizes(0);
            }
        }

        private static int GetItemOrigem(string item_code)
        {
            if (string.IsNullOrEmpty(item_code) || item_code == "*")
                return 0;

            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = $"Select ProductSrc FROM OITM WHERE ItemCode = '{item_code}'";
            recordset.DoQuery(query);
            int value = Convert.ToInt32(recordset.Fields.Item(0).Value);
            if (recordset is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Marshal.ReleaseComObject(recordset);
            return value;
        }

        /*
         * Cadastra os regimes tributários 
         * utiliza a tabela OBNI do index type = 18 
         * mas somente código 11 e 12 possuem regras
         * mas a matriz toda deve ser cadastrada
        */
        private static TaxRegime GetRegime(Recordset recordset)
        {
            string query = "Select \"Code\", \"Descr\", \"U_F1_ID\" FROM OBNI WHERE \"IndexType\" = 18";
            recordset.DoQuery(query);
            TaxRegime regime = new(recordset.RecordCount);
            int count = 0;
            while (!recordset.EoF)
            {
                regime.data.Add(new Regime()
                {
                    code = Convert.ToString(recordset.Fields.Item(0).Value),
                    name = Convert.ToString(recordset.Fields.Item(1).Value),
                    modified = 0,
                    // formato = 2019-04-21
                    export_date = DateTime.Now.ToString("yyyy-MM-dd"),
                    F1_ID = Convert.ToInt32(recordset.Fields.Item(2).Value),
                });

                count++;
                recordset.MoveNext();
            }

            return regime;
        }

        public void CadastrarRegimesTributarios()
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            TaxRegime regime = GetRegime(recordset);
            TaxRegime newRegimes = new(0)
            {
                data = regime.data.Where(x => x.F1_ID == 0).ToList()
            };
            _logger.LogInformation("Cadastrando {qtd} regimes tributários", newRegimes.data.Count);

            CadastrarRegimesTributarios(newRegimes, recordset);
            UpdateRegimesTributarios(regime.data.Where(x => x.F1_ID > 0).ToArray());
        }

        private async void UpdateRegimesTributarios(Regime[] regimes)
        {
            // mesmo em caso de erro 
            try
            {
                if (regimes.Length < 1)
                    return;

                _logger.LogInformation("Atualizando {qtd} regimes tributários", regimes.Length);
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = TAX_REGIME_URL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");
                for(int index = 0; index < regimes.Length; index++)
                {
                    string uri = $"{url}/{regimes[index].code}";
                   _ = await _connection.PUT<Regime, Regime>(uri, header, regimes[index]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar Atualizar o regime tributário: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        private async void CadastrarRegimesTributarios(TaxRegime regime, Recordset recordset)
        {
            // mesmo em caso de erro 
            try
            {
                if (regime.data.Count < 1)
                    return;
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = TAX_REGIME_URL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");
                RegimeResponse[]? data = await _connection.POST<TaxRegime, RegimeResponse[]>(url, header, regime);
                if(data is null)
                {
                    _logger.LogError("Erro ao tentar cadastrar o regime tributário: retorno nulo");
                    return;
                }

                for(int index = 0; index < data.Length; index++)
                {
                    string query = $"Update OBNI set U_F1_ID = {data[index].id} WHERE Code = '{data[index].code}' AND \"IndexType\" = 18";
                    recordset.DoQuery(query);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar cadastrar o regime tributário: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }
    }
}