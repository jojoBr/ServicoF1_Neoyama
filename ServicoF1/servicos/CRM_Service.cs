using SAPbobsCOM;
using ServicoF1.Enums;
using ServicoF1.Models.F1.Condicao_de_Pagamento;
using ServicoF1.Models.F1.CRM;
using ServicoF1.Models.F1.CRMContacts;
using ServicoF1.Models.F1.CRMExterno;
using ServicoF1.Models.F1.Segmentos_CRM;
using ServicoF1.Models.F1.Sellers;
using ServicoF1.Models.WEB;
using ServicoF1.Uteis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Collections.Generic;


namespace ServicoF1.servicos
{
    public sealed class CRM_Service
    {
        private const string CRM_URL = "https://apidatain-prod.f1ws.com.br/api/crm";
        private const string PAYMENT_CONDITIONS_URL = "https://apidatain-prod.f1ws.com.br/api/payment_conditions";
        private const string Contacts_URL = "https://apidatain-prod.f1ws.com.br/api/crmcontacts";
        private const string Sellers_URL = "https://apidatain-prod.f1ws.com.br/api/sellers";
        private const string Wallets_URL = "https://apidatain-prod.f1ws.com.br/api/wallets";
        private const string Segments_URL = "https://apidatain-prod.f1ws.com.br/api/crmsegments";
        private readonly ILogger _logger;
        private readonly API.ApiConnection _connection;
        private readonly string _token;
        private readonly bool _homolog;

        public CRM_Service(ILogger logger, string token, bool homolog)
        {
            _logger = logger;
            _token = token;
            _homolog = homolog;
            _connection = new API.ApiConnection(_token, _logger);
        }

        public void Run(bool DataSendIntoLogger, bool update = false)
        {
            CadastraVendedoresECarteiras(DataSendIntoLogger, update);
            CadastraCondicaoDePagamento(DataSendIntoLogger, update);
            //CadastraSegmentos();
            if (!update)
                CadastraCrm(DataSendIntoLogger);
            else
                UpdateCrm(DataSendIntoLogger);
            CadastraParceirosF1ParaSAP();
        }

        public void CadastraParceirosF1ParaSAP()
        {
            Span<CrmExterno> clients = GetClients();
            foreach(var client in clients)
            {
                try
                {
                    if (client is null || string.IsNullOrEmpty(client.cgc) || !string.IsNullOrEmpty(client.code))
                        continue;
                    _ = CadastrarClienteF1ParaSAP(client.cgc, "").Result;
                }
                catch(Exception ex)
                {
                    _logger.LogError("Erro ao tentar adicionar um parceiro de cnpj {cnpj} novo para o SAP: {erro}", client.cgc, ex.Message);
                    if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                    {
                        DIAPI.API.Reset = true;
                    }
                }
            }
        }

        private Span<CrmExterno> GetClients()
        {
            Header[] headers = new Header[1] { new Header("Bearer", _token) };
            string dataFilter = $"?updated_for=APISYNC";//filterStartDate={DateTime.Now.AddDays(-1):yyyy-MM-dd}&filterEndDate={DateTime.Now.AddDays(1):yyyy-MM-dd}&
            string url = CRM_URL + dataFilter;
            if (_homolog)
                url = url.Replace("apidatain-prod", "apidatain-homolog");
            CrmsExterno? clients = _connection.GET<CrmsExterno>(url, headers).Result;
            if(clients is null)
                return new Span<CrmExterno>();

            while (!string.IsNullOrEmpty(clients.next_page_url))
            {
                CrmsExterno? nextPageClients = _connection.GET<CrmsExterno>(clients.next_page_url, headers).Result;
                clients.data.AddRange(nextPageClients!.data);
                clients.next_page_url = nextPageClients.next_page_url;
            }

            if (clients.data.Count > 0)
                _logger.LogInformation("Foram Encontrados novos {size} parceiros para cadastrar", clients.data.Count);
            return CollectionsMarshal.AsSpan(clients.data);
        }

        public async void CadastraCondicaoDePagamento(bool dataSendIntoLogger, bool update)
        {
            try
            {
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = PAYMENT_CONDITIONS_URL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");

                if (update)
                {
                    _logger.LogInformation("Atualizando condições de pagamento");
                    CondicaoDePagamento condicoesUpdate = GetCondicoes();
                    foreach (var condicao in condicoesUpdate.data)
                    {
                        try
                        {
                            string uri = $"{url}/{condicao.code}";
                            _ = await _connection.PUT<Condicao, Condicao>(uri, header, condicao);
                        }
                        catch(Exception ex)
                        {
                            _logger.LogError("Erro ao tentar Atualizar a condição de pagamento {condicao}: {erro}", condicao.code, ex.Message);
                            if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                            {
                                DIAPI.API.Reset = true;
                            }
                        }
                    }
                    return;
                }

                Condicao? BOLETO = await BoletoExist(url, header);
                if (BOLETO != null)
                    return;

                _logger.LogInformation("Adicionando condições de pagamento");
                CondicaoDePagamento condicoes = GetCondicoes();
                if (condicoes.data.Length < 1)
                    return;

                if (dataSendIntoLogger)
                    _logger.LogInformation("Data send for add: {data}", JsonSerializer.Serialize(condicoes));

                Condicao[]? response = await _connection.POST<CondicaoDePagamento, Condicao[]>(url, header, condicoes);
                PaymentTermsTypes paymentTerms = (PaymentTermsTypes)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oPaymentTermsTypes);
                foreach (var condition in condicoes.data)
                {
                    Condicao? condicao = await _connection.GET<Condicao>(url + $"/{condition.code}", header);
                    if (condicao is null)
                        continue;
                    foreach (var installment in condicao.installments)
                    {
                        if (paymentTerms.GetByKey(Convert.ToInt32(installment.code)))
                        {
                            paymentTerms.UserFields.Fields.Item("U_F1_Id").Value = installment.id;
                            paymentTerms.Update();
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao tentar cadastrar as condições de pagamento: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        private async Task<Condicao?> BoletoExist(string url, Header[] header)
        {
            //Condicao? CARTAO = await _connection.GET<Condicao>(url + $"/CARTAO", header);
            //caso não exista cadastra a condição
            try
            {
                Condicao? BOLETO = await _connection.GET<Condicao>(url + $"/BOLETO", header);
                return BOLETO;
            }
            catch (Exception)
            {
                _logger.LogInformation("Boleto Ainda Não Cadastrado preparando cadastro");
                return null;
            }
        }

        private CondicaoDePagamento GetCondicoes(bool add_bol = true)
        {
            if (!add_bol)
                return new CondicaoDePagamento();

            string query = $@"SELECT T0.GroupNum, T0.PymntGroup, T0.InstNum, /*T1.IntsNo, T1.InstPrcnt, */T0.ExtraDays, T0.ExtraMonth, U_TipoPagamento, ISNULL(Cast(U_ParcelaMinima as CHAR), ''), U_Status_Inicial FROM OCTG T0 
                            /*LEFT JOIN CTG1 T1 ON T0.GroupNum = T1.CTGCode*/  WHERE ";

            //if (add_card && !add_bol)
            //    query += "U_TipoPagamento = 'CARTAO'";
            //else if (add_bol && !add_card)
            //    query += "U_TipoPagamento = 'BOLETO'";
            //else
            query += "U_TipoPagamento = 'BOLETO'";

            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            recordset.DoQuery(query);
            if(recordset.RecordCount < 1)
                return new CondicaoDePagamento();

            Dictionary<string, Condicao> condicoes = new Dictionary<string, Condicao>();
            while (!recordset.EoF)
            {
                string tipo = recordset.Fields.Item(5).Value;
                string name = recordset.Fields.Item(1).Value;
                int code = recordset.Fields.Item(0).Value;
                string tiponame = recordset.Fields.Item(5).ValidValue;
                if (condicoes.TryGetValue(tipo, out Condicao? value) && value is not null)
                {
                    value.installments.Add(new Installment()
                    {
                        code = $"{code}",
                        installment_number = (int)recordset.Fields.Item(2).Value,
                        minimum_value = ((string)recordset.Fields.Item(6).Value).Replace(",", "."),
                        display = $"-{name}",
                        type = "",
                        average_period_payment = GetDaysFromPayConditions(code.ToString()),
                        percent = "0",//GetPercentAsString((int)recordset.Fields.Item(4).Value, value, code.ToString()),
                        post_payment = 1,//(Convert.ToInt32(recordset.Fields.Item(3).Value) > 0 || Convert.ToInt32(recordset.Fields.Item(4).Value) > 0) ? 1 : 0,
                        initial_status = string.IsNullOrEmpty(recordset.Fields.Item(7).Value) ? "" : (string)recordset.Fields.Item(7).Value
                    });
                    recordset.MoveNext();
                    continue;
                }

                int? number = (int?)recordset.Fields.Item(2).Value;
                Condicao condicao = new()
                {
                    code = tipo,
                    type = tiponame,
                    name = tiponame,
                    installments_number = (int)recordset.Fields.Item(2).Value,
                    installments = new List<Installment>()
                    {
                        new Installment()
                        {
                            code = $"{code}",
                            display = $"-{name}",
                            minimum_value = ((string)recordset.Fields.Item(6).Value).Replace(",", "."),
                            type = "",
                            installment_number = number is null ? 1 : (int)number,
                            average_period_payment = GetDaysFromPayConditions(code.ToString()),
                            percent = "0",//(((object)recordset.Fields.Item(4).Value) == null || (int)recordset.Fields.Item(4).Value <= 0) ? 100.ToString() : Math.Ceiling((double)recordset.Fields.Item(4).Value).ToString(),
                            post_payment = 1,//(Convert.ToInt32(recordset.Fields.Item(3).Value) > 0 || Convert.ToInt32(recordset.Fields.Item(4).Value) > 0) ? 1 : 0,
                            initial_status = string.IsNullOrEmpty(recordset.Fields.Item(7).Value) ? "" : (string)recordset.Fields.Item(7).Value
                        }
                    }
                };
                condicoes.Add(tipo, condicao);
                recordset.MoveNext();
            }

            CondicaoDePagamento condicaoDePagamento = new();
            condicaoDePagamento.data = condicoes.Values.ToArray();
            return condicaoDePagamento;
        }

        public string GetPercentAsString(int value, Condicao condicao, string code)
        {
            int total = condicao.installments.Where(x => x.code.Contains($"{code}-")).Sum(x => int.Parse(x.percent));
            if (total + value >= 100)
                return (100 - total).ToString();

            return value.ToString();
        }

        public async void UpdateCrm(bool DataSendIntoLogger)
        {
            // busca os dados
            Recordset? recordset = default;
            BusinessPartners? partners = default;
            try
            {
                recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                partners = (BusinessPartners)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oBusinessPartners);

                CRMS crms = BuscaCRM(recordset, SearchType.Update);
                if(DataSendIntoLogger)
                    _logger.LogInformation("Data send for add: {data}", JsonSerializer.Serialize(crms));
                CRMsContacts contacts = BuscaContatos(recordset, SearchType.Update);
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = CRM_URL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");

                _logger.LogInformation("Atualizando {count} parceiros", crms.data.Count);
                for (int index = 0; index < crms.data.Count; index++)
                {
                    try
                    {
                        string uri = url + $"/{crms.data[index].code}";
                        // envia os dados
                        _ = await _connection.PUT<CRM, CRM[]>(uri, header, crms.data[index]);
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError("erro ao tentar Atualizar o parceiro {partner} e seus contatos: {erro}", crms.data[index].code, ex.Message);
                        if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                        {
                            DIAPI.API.Reset = true;
                        }
                    }
                }

                url = Contacts_URL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");

                _logger.LogInformation("Atualizando {count} contatos dos parceiros", contacts.data.Count);
                for (int index = 0; index < contacts.data.Count; index++)
                {
                    string uri = url + $"/{contacts.data[index].mail}";
                    _ = await _connection.PUT<CRMContacts, CRMContacts>(uri, header, contacts.data[index]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("erro ao tentar Atualizar os parceiros e seus contatos: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
            finally
            {
                if (partners is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.FinalReleaseComObject(partners);
                if (recordset is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.FinalReleaseComObject(recordset);
            }
        }

        /// <summary>
        /// Metodo que cadastra os vendedores
        /// segundo uma reunião no dia 27/10/2022 foi aprovado o uso do vendedor como carteira
        /// mas as carteiras terão o prefico Carteira- na frente do nome do vendedor
        /// </summary>
        public async void CadastraVendedoresECarteiras(bool DataSendIntoLogger, bool update = false)
        {
            try
            {
                GetSellers(update, out Sellers sellers, out Wallets wallets);
                if (wallets.Data.Length < 1)
                    return;

                string walletUrl = Wallets_URL;
                if (_homolog)
                    walletUrl = walletUrl.Replace("apidatain-prod", "apidatain-homolog");
                string sellerUrl = Sellers_URL;
                if (_homolog)
                    sellerUrl = sellerUrl.Replace("apidatain-prod", "apidatain-homolog");

                if (update)
                {
                    await AtualizaVendedoresECarteiras(sellers, wallets, walletUrl, sellerUrl, DataSendIntoLogger);
                }
                else
                {
                    await CadastraVendedoresECarteiras(sellers, wallets, walletUrl, sellerUrl, DataSendIntoLogger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("erro ao Cadastrar os vendedores: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        private async Task AtualizaVendedoresECarteiras(Sellers sellers, Wallets wallets, string walletUrl, string sellerUrl, bool DataSendIntoLogger)
        {
            Header[] header = new Header[1] { new Header("Bearer", _token) };
            _logger.LogInformation("Atualizando {num} carteiras", wallets.Data.Length);
            for (int index = 0; index < wallets.Data.Length; index++)
            {
                string uri = walletUrl + $"/{wallets.Data[index].code}";
                _ = await _connection.PUT<Models.F1.Sellers.Wallet, Models.F1.Sellers.Wallet>(uri, header, wallets.Data[index]);
            }

            // Cadastro de vendedores, caso ocorra erro com o cadastro de carteiras os vendedores não serão cadastrados
            _logger.LogInformation("Atualizando {num} vendedores", sellers.Data.Length);
            for (int index = 0; index < sellers.Data.Length; index++)
            {
                string uri = sellerUrl + $"/{sellers.Data[index].code}";
                if(DataSendIntoLogger)
                    _logger.LogInformation("dados do vendedor: {data}", JsonSerializer.Serialize(sellers.Data[index]));
                _ = await _connection.PUT<Models.F1.Sellers.Seller, Models.F1.Sellers.Seller>(uri, header, sellers.Data[index]);
            }

            _logger.LogInformation("Atualização de vendedores feita com sucesso");
        }

        private async Task CadastraVendedoresECarteiras(Sellers sellers, Wallets wallets, string walletUrl, string sellerUrl, bool DataSendIntoLogger)
        {
            Header[] header = new Header[1] { new Header("Bearer", _token) };
            _ = await _connection.POST<Wallets, Models.F1.Sellers.Wallet[]>(walletUrl, header, wallets);
            // Cadastro de vendedores, caso ocorra erro com o cadastro de carteiras os vendedores não serão cadastrados

            if (DataSendIntoLogger)
                _logger.LogInformation("dados dos vendedores: {data}", JsonSerializer.Serialize(sellers));
            Models.F1.Sellers.Seller[]? response = await _connection.POST<Sellers, Models.F1.Sellers.Seller[]>(sellerUrl, header, sellers);
            if(response is not null && response.Length == 0)
            {
                SalesPersons salesEmployee = (SalesPersons)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oSalesPersons);
                foreach (var seller in sellers.Data)
                {
                    if (salesEmployee.GetByKey(Convert.ToInt32(seller.code)))
                    {
                        salesEmployee.UserFields.Fields.Item("U_F1_Id").Value = "f1";
                        salesEmployee.Update();
                    }
                }
            }
            else
            {
                _logger.LogError("Erro ao recupar os dados de cadastro de vendedores, o valor retornado foi nulo");
                return;
            }

            _logger.LogInformation("cadastro dos vendedores feito com sucesso");
        }

        private void GetSellers(bool update, out Sellers sellers, out Wallets wallets)
        {
            Recordset? recordset = null;
            try
            {
                recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string query;
                if(update)
                    query = "SELECT \"SlpCode\", \"SlpName\", \"Email\", \"Active\" FROM OSLP WHERE ISNULL(\"U_F1_Id\", '') <> '' and  ISNULL(\"Email\", '') <> ''";
                else
                    query = "SELECT \"SlpCode\", \"SlpName\", \"Email\", \"Active\" FROM OSLP WHERE ISNULL(\"U_F1_Id\", '') = '' and  ISNULL(\"Email\", '') <> ''";

                recordset.DoQuery(query);
                sellers = new Sellers(recordset.RecordCount);
                wallets = new Wallets(recordset.RecordCount);
                int index = 0;
                while (!recordset.EoF)
                {
                    wallets.Data[index] = new Models.F1.Sellers.Wallet()
                    {
                        code = Convert.ToString(recordset.Fields.Item(0).Value),
                        name = "Carteira-" + recordset.Fields.Item(2).Value,
                        active = (string)recordset.Fields.Item(3).Value == "Y" ? 1 : 0,
                    };

                    Models.F1.Sellers.Seller seller = new()
                    {
                        code = Convert.ToString(recordset.Fields.Item(0).Value),
                        name = recordset.Fields.Item(1).Value,
                        email = recordset.Fields.Item(2).Value,
                        active = (string)recordset.Fields.Item(3).Value == "Y" ? 1 : 0,
                    };

                    seller.wallets[0] = new Models.F1.Sellers.Wallet() { code = seller.code };
                    sellers.Data[index] = seller;
                    index++;
                    recordset.MoveNext();
                }

                _logger.LogInformation("busca de dados dos vendedores feita com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError("erro ao buscar os vendedores: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                sellers = new Sellers(0);
                wallets = new Wallets(0);
            }
            finally
            {
                if (recordset is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.FinalReleaseComObject(recordset);
            }
        }

        public async void  CadastraCrm(bool DataSendIntoLogger)
        {
            // busca os dados
            // busca os dados
            Recordset? recordset = default;
            BusinessPartners? partners = default;
            try
            {
                recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                CRMS crms = BuscaCRM(recordset);
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = CRM_URL;

                if (crms.data.Count > 0) {
                    if (_homolog)
                        url = url.Replace("apidatain-prod", "apidatain-homolog");
                    // envia os dados
                    if (DataSendIntoLogger)
                        _logger.LogInformation("Data send for add: {data}", JsonSerializer.Serialize(crms));
                    CRM[]? response = await _connection.POST<CRMS, CRM[]>(url, header, crms);
                    if (response is not null)
                    {
                        partners = (BusinessPartners)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oBusinessPartners);
                        List<CRM> data = crms.data.ToList();
                        foreach (CRM crm in data)
                        {
                            if (response.Any(x => x.code == crm.code))
                                continue;

                            int id = GetPartnerID(url, header, crm.code!);
                            if (id != -1 && partners.GetByKey(crm.code))
                            {
                                partners.UserFields.Fields.Item("U_F1_Id").Value = id.ToString();
                                partners.Update();
                            }
                        }

                        for (int index = 0; index < response.Length; index++)
                        {
                            if (partners.GetByKey(response[index].code))
                            {
                                partners.UserFields.Fields.Item("U_F1_Id").Value = response[index].id.ToString();
                                partners.Update();
                            }
                        }

                        _logger.LogInformation("cadastro dos parceiros feito com sucesso");
                    }
                    else
                        _logger.LogError("Erro ao recupar os dados de cadastro do CRM, o valor retornado foi nulo");
                }

                CRMsContacts contacts = BuscaContatos(recordset);
                if (contacts.data.Count > 0)
                {
                    url = Contacts_URL;
                    if (_homolog)
                        url = url.Replace("apidatain-prod", "apidatain-homolog");

                    CRMContacts[]? lotes = await _connection.POST<CRMsContacts, CRMContacts[]>(url, header, contacts);

                    if (lotes is not null)
                    {
                        List<CRMContacts> data = lotes.ToList();
                        foreach (CRMContacts contact in data)
                        {
                            if (lotes.Any(x => x.mail == contact.mail))
                                continue;

                            int id = GetContactID(url, header, contact.mail);
                            if (id != -1)
                            {
                                recordset.DoQuery($"Update OCPR  SET U_F1_Id = {id} FROM OCPR T0 LEFT JOIN CRD7 T1 ON T0.CardCode = T1.CardCode WHERE T0.E_MailL = '{contact.mail}'");
                            }
                        }


                        foreach (var lote in lotes)
                        {
                            string mail = lote.mail;
                            int? id = lote.id;
                            recordset.DoQuery($"Update OCPR  SET U_F1_Id = {id} FROM OCPR T0 LEFT JOIN CRD7 T1 ON T0.CardCode = T1.CardCode WHERE T0.E_MailL = '{mail}'"); //AND (TaxId0 = '{crm.cgc}' OR TaxId4 = '{crm.cgc}')
                        }

                        _logger.LogInformation("cadastro dos contatos feito com sucesso");
                    }
                    else
                        _logger.LogError("Erro ao recupar os dados de cadastro do CRM, o valor retornado foi nulo");
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("erro ao tentar cadastrar os parceiros e seus contatos: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
            finally
            {
                if (partners is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.FinalReleaseComObject(partners);
                if (recordset is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.FinalReleaseComObject(recordset);
            }
        }

        private int GetContactID(string url, Header[] header, string mail)
        {
            try
            {
                CRMContacts? contact = _connection.GET<CRMContacts>(url + $"/{mail}", header).Result;
                if(contact == null)
                    return -1;
                return (int)contact.id!;
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao bucar o contato {mail}, erro: {error}", mail, ex.Message);
                return -1;
            }
        }

        private int GetPartnerID(string url, Header[] header, string code)
        {
            try
            {
                CRM? crm = _connection.GET<CRM>(url + $"/{code}", header).Result;
                if(crm == null)
                    return -1;
                return (int)crm.id!;
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao bucar o cliente {code}, erro: {error}", code, ex.Message);
                return -1;
            }
        }

        /*
         * ENUM Simples Nacional
         * O - selecione
         * N - Não Participa
         * P - Participa
         * A - Em Andamento
         * I - Inválido
         */

        public CRMS BuscaCRM(Recordset record, SearchType type = SearchType.Add)
        {
            try
            {
                CRMS jsonCrmObjetct = new();
                string anexo;
                if (type == SearchType.Update)
                    anexo = "AND ISNULL(T0.U_F1_Id, '') <> '' AND T0.U_CadastraF1 = 'Y'";
                else
                    anexo = "AND ISNULL(T0.U_F1_Id, '') = '' AND T0.U_CadastraF1 = 'Y'";
                string query = Queries.Busca_Parceiro.Replace("@anexo", anexo);
                // para utilizar no recordset basta usar os numeros das colunas -1
                record.DoQuery(query);
                Dictionary<string, CRM> crms = new();
                int index = 0;
                while (!record.EoF)
                {
                    if (!crms.TryGetValue((string)record.Fields.Item(0).Value, out CRM _))
                    {
                        CRM crm = new();
                        // dados gerais
                        crm.cgc = record.Fields.Item(0).Value;
                        crm.code = record.Fields.Item(1).Value;
                        crm.person_type = record.Fields.Item(2).Value;
                        crm.tax_regime = Convert.ToString(record.Fields.Item(3).Value);
                        crm.name_fancy = record.Fields.Item(4).Value;
                        crm.name_corporate = record.Fields.Item(5).Value;
                        crm.wallets = new Models.F1.CRM.Wallet[1]
                        {
                            new Models.F1.CRM.Wallet(Convert.ToString(record.Fields.Item(56).Value))
                        };
                        if (string.IsNullOrEmpty(crm.name_fancy))
                            crm.name_fancy = crm.name_corporate;
                        crm.registration_state = record.Fields.Item(6).Value;
                        crm.registration_municipal = record.Fields.Item(7).Value;
                        crm.suframa_code = record.Fields.Item(8).Value;
                        crm.suframa_expiration_date = record.Fields.Item(9).Value;
                        crm.phone_number = record.Fields.Item(10).Value;
                        crm.cell_phone_number = record.Fields.Item(11).Value;
                        crm.email_invoice = ((string)record.Fields.Item(12).Value).Split(';').First().Split(',').First();
                        crm.email = ((string)record.Fields.Item(65).Value).Split(';').First().Split(',').First();
                        if (string.IsNullOrEmpty(crm.email))
                            crm.email = crm.email_invoice;

                        crm.external_segment_code = record.Fields.Item(13).Value;
                        crm.price_list = Convert.ToString(record.Fields.Item(14).Value);
                        double credito = Convert.ToDouble(record.Fields.Item(15).Value, new CultureInfo("en-US"));
                        double balance = Convert.ToDouble(record.Fields.Item(58).Value);
                        double on_Orders_balance = Convert.ToDouble(record.Fields.Item(62).Value);
                        if ((balance + on_Orders_balance) > credito)
                            crm.credit = 0;
                        else
                            crm.credit = credito - (balance + on_Orders_balance);
                        int averagePaymentPeriod = GetDaysFromPayConditions(Convert.ToString(record.Fields.Item(16).Value));
                        crm.average_payment_period = averagePaymentPeriod == 0 ? "" : averagePaymentPeriod.ToString();
                        if (!string.IsNullOrEmpty((string)crm.average_payment_period) && !HasOrderToPay(crm.code))
                        {
                            crm.allow_post_payment = (balance + on_Orders_balance) < credito ? 1 : 0;
                        }
                        else
                            crm.allow_post_payment = 0;
                        crm.default_order_type = record.Fields.Item(18).Value;
                        crm.default_operation_type = record.Fields.Item(19).Value;
                        crm.exclusive_payment_method_condition_installment =  record.Fields.Item(20).Value;
                        crm.simples_code = record.Fields.Item(21).Value;
                        crm.moderated = record.Fields.Item(22).Value == "Y" ? 1 : 0;
                        crm.created = record.Fields.Item(23).Value;
                        crm.address_street = record.Fields.Item(24).Value;
                        crm.address_number = record.Fields.Item(25).Value;
                        crm.address_complement = record.Fields.Item(26).Value;
                        crm.address_state = record.Fields.Item(27).Value;
                        crm.address_city = record.Fields.Item(28).Value;
                        crm.address_neighborhood = record.Fields.Item(29).Value;
                        crm.address_country = record.Fields.Item(30).Value;
                        crm.address_reference = record.Fields.Item(31).Value;
                        crm.address_zipcode = record.Fields.Item(32).Value;
                        string simples = record.Fields.Item(59).Value;
                        if (simples.Equals("S") || simples.Equals("X"))
                            crm.simples_code = "P";
                        else
                            crm.simples_code = "N";
                        // endereço de cobrança
                        crm.charge_address_street = record.Fields.Item(33).Value;
                        crm.charge_address_number = record.Fields.Item(34).Value;
                        crm.charge_address_complement = record.Fields.Item(35).Value;
                        crm.charge_address_state = record.Fields.Item(36).Value;
                        crm.charge_address_city = record.Fields.Item(37).Value;
                        crm.charge_address_neighborhood = record.Fields.Item(38).Value;
                        crm.charge_address_country = record.Fields.Item(39).Value;
                        crm.charge_address_reference = record.Fields.Item(40).Value;
                        crm.charge_address_zipcode = record.Fields.Item(41).Value;
                        // endereço de entrega
                        crm.delivery_address_street = record.Fields.Item(24).Value;
                        crm.delivery_address_number = record.Fields.Item(25).Value;
                        crm.delivery_address_complement = record.Fields.Item(26).Value;
                        crm.delivery_address_state = record.Fields.Item(27).Value;
                        crm.delivery_address_city = record.Fields.Item(28).Value;
                        crm.delivery_address_neighborhood = record.Fields.Item(29).Value;
                        crm.delivery_address_country = record.Fields.Item(30).Value;
                        crm.delivery_address_reference = record.Fields.Item(31).Value;
                        crm.delivery_address_zipcode = record.Fields.Item(32).Value;
                        crm.active = Convert.ToString(record.Fields.Item(57).Value) == "Y" ? 1 : 0;
                        crm.customfields = new Customfield[2];
                        crm.customfields[0] = new Customfield()
                        {
                            identification = "Indicador da IE",
                            label = "Indicador da IE",
                            value = Convert.ToString(record.Fields.Item(60).Value)
                        };
                        crm.customfields[1] = new Customfield()
                        {
                            identification = "Indicador de OP. Consumidor",
                            label = "Indicador de OP. Consumidor",
                            value = Convert.ToString(record.Fields.Item(61).Value)
                        };
                        string val = Convert.ToString(record.Fields.Item(63).Value);
                        string[] types = string.IsNullOrEmpty(val) ? Array.Empty<string>() : val.Split(";").Where(x => !string.IsNullOrEmpty(x)).ToArray();
                        crm.ordertypes = new Odertypes[types.Length];
                        for (int i = 0; i < types.Length; i++)
                        {
                            crm.ordertypes[i] = new Odertypes()
                            {
                                code = types[i],
                                name = types[i],
                                price_list_external_code = Convert.ToString(record.Fields.Item(14).Value),
                                price_list_name = Convert.ToString(record.Fields.Item(64).Value)
                            };
                        }

                        crms.Add((string)record.Fields.Item(0).Value, crm);
                    }

                    record.MoveNext();
                    index++;
                }

                jsonCrmObjetct.data = crms.Values.ToList();
                _logger.LogInformation("busca de dados de {count} parceiros feita com sucesso", jsonCrmObjetct.data.Count);
                return jsonCrmObjetct;
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao buscar os dados de parceiros: {error}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }

            return new CRMS();
        }

        public bool HasOrderToPay(string code)
        {
            Recordset recordset = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            try
            {
                string query = Queries.ContasEmAtraso.Replace("@Code", code);
                recordset.DoQuery(query);
                if(recordset.RecordCount > 0)
                {
                    if(Convert.ToInt32(recordset.Fields.Item(1).Value) > 0)
                        return true;
                    return false;
                }

                return false;
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao tentar verificar se o parceiro {code} possuia pagamentos em atraso: {erro}", code, ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return false;
            }
            finally
            {
                if (recordset != null)
                    Marshal.ReleaseComObject(recordset);
            }
        }

        public int GetDaysFromPayConditions(string code)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            try
            {
                string query = $"SELECT FLOOR(SUM(InstDays)/ COUNT(InstDays)) FROM CTG1 WHERE CTGCode = {code}";
                recordset.DoQuery(query);
                if(recordset.RecordCount > 0)
                    return (int)recordset.Fields.Item(0).Value;

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar buscar os dias do pagamento: {error}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return 0;
            }
            finally
            {
                if(recordset is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }
        }

        public CRMsContacts BuscaContatos(Recordset record, SearchType type = SearchType.Add)
        {
            try
            {
                string anexo;
                if (type == SearchType.Update)
                    anexo = "AND ISNULL(T0.U_F1_Id, '') <> '' AND ISNULL(T1.U_F1_Id, '') <> ''";
                else
                    anexo = "AND ISNULL(T0.U_F1_Id, '') = '' AND ISNULL(T1.U_F1_Id, '') <> ''";
                string query = Queries.Busca_Contatos.Replace("@anexo", anexo);
                record.DoQuery(query);
                Dictionary<int, CRMContacts> contacts = new();
                while (!record.EoF)
                {
                    if (!contacts.TryGetValue((int)record.Fields.Item(12).Value, out _))
                    {
                        CRMContacts newContact = new();
                        newContact.active = ((string)record.Fields.Item(0).Value) == "Y" ? 1 : 0;
                        newContact.sector = record.Fields.Item(1).Value;
                        newContact.name = record.Fields.Item(2).Value;
                        newContact.mail = record.Fields.Item(3).Value;
                        newContact.is_telesales = record.Fields.Item(4).Value;
                        newContact.purchasing_limit = record.Fields.Item(5).Value;
                        newContact.visualize_only_owned_orders = record.Fields.Item(6).Value;
                        newContact.permission_place_orders = record.Fields.Item(7).Value;
                        newContact.permission_open_rma = record.Fields.Item(8).Value;
                        newContact.permission_get_orders = record.Fields.Item(9).Value;
                        newContact.permission_visualize_prices = record.Fields.Item(10).Value;
                        newContact.permission_allow_orders_ignoring_purchasing_limit = record.Fields.Item(11).Value;
                        newContact.ContatctCode = record.Fields.Item(12).Value;
                        newContact.Crms = new List<Crm>
                        {
                            new Crm()
                            {
                                cgc = Convert.ToString(record.Fields.Item(13).Value),//VerificaCNPJ(record.Fields.Item(13).Value)
                            }
                        };

                        contacts.Add(record.Fields.Item(12).Value, newContact);
                    }

                    record.MoveNext();
                }

                CRMsContacts cRMsContacts = new()
                {
                    data = contacts.Values.ToList()
                };
                _logger.LogInformation("busca de {count} dados dos contatos feita com sucesso", cRMsContacts.data.Count);
                return cRMsContacts;
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao buscar os dados de contatos: {error}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }


            return new CRMsContacts();
        }

        private string VerificaCNPJ(string cnpj)
        {
            string result = cnpj;
            if(result.Length < 15)
            {
                result = result.Replace(".", "").Replace("/", "").Replace("-", "");
                result = result.Substring(0, 2) + "." + result.Substring(2, 3) + "." + result.Substring(5, 3) + "/" + result.Substring(8, 4) + "-" + result.Substring(12, 2);
            }

            return result;
        }

        public async Task<string> CadastrarClienteF1ParaSAP(string CNPJ, string paymentMethod)
        {
            string url = $"{CRM_URL}/{CNPJ}";
            if (_homolog)
                url = url.Replace("apidatain-prod", "apidatain-homolog");
            Header[] header = new Header[1] { new Header("Bearer", _token) };
            CrmExterno? cliente = await _connection.GET<CrmExterno>(url, header) ?? throw new Exception($"Não foi possivel encontrar o cliente com CNPJ: {CNPJ}, verifique se a api esta funcionando corretamente");

            if (Exists(CNPJ, out string code))
            {
                //CRM? BP = await _connection.GET<CRM>(url, header);
                //UpdateBP(code, BP!, paymentMethod);
#if RELEASE
                cliente.code = code;
                _logger.LogInformation("cliente {key} Atualizado com sucesso atualizando dados", code);
                _ = await _connection.PUTString(url, header, cliente);
#endif
                return code;
            }

            BusinessPartners partner = (BusinessPartners)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oBusinessPartners);
            partner.CardName = cliente.name_corporate;
            partner.AliasName = cliente.name_fancy;
            partner.CardForeignName = cliente.name_fancy;
            partner.Phone1 = cliente.phone_number[2..];
            partner.Phone2 = cliente.phone_number[..2];
            partner.EmailAddress = cliente.email_invoice;
            partner.UserFields.Fields.Item("U_email_f1").Value = cliente.email;
            partner.SinglePayment = BoYesNoEnum.tYES;
            partner.CardType = BoCardTypes.cCustomer;
#if DEBUG
            partner.Series = 56;// teste 56
#elif RELEASE
            partner.Series = 1;// neoyama Manual
            partner.CardCode = GetNewCode();
#endif
            // endereço de envio
            partner.Addresses.AddressName = cliente.delivery_address_street!.ToUpper();
            partner.Addresses.AddressType = BoAddressType.bo_ShipTo;
            partner.Addresses.TypeOfAddress = cliente.delivery_address_street.Contains("Avenida", StringComparison.OrdinalIgnoreCase) ? "Avenida" : "Rua";
            partner.Addresses.Street = cliente.delivery_address_street!;
            partner.Addresses.StreetNo = cliente.delivery_address_number!;
            partner.Addresses.State = cliente.delivery_address_state!;
            partner.Addresses.Country = GetCountryId(cliente.delivery_address_country!);
            partner.Addresses.ZipCode = cliente.delivery_address_zipcode!;
            partner.Addresses.Block = cliente.delivery_address_neighborhood!;
            partner.Addresses.BuildingFloorRoom = cliente.delivery_address_complement!;
            partner.Addresses.City = cliente.delivery_address_city!;
            partner.Addresses.County = GetCityByNameAndState(partner.Addresses.State, partner.Addresses.City);
            // endereço de cobrança
            partner.Addresses.Add();
            partner.Addresses.AddressName = cliente.charge_address_street!.ToUpper();
            partner.Addresses.TypeOfAddress = cliente.delivery_address_street.Contains("Avenida", StringComparison.OrdinalIgnoreCase) ? "Avenida" : "Rua";
            partner.Addresses.AddressType = BoAddressType.bo_BillTo;
            partner.Addresses.Street = cliente.charge_address_street!;
            partner.Addresses.StreetNo = cliente.charge_address_number!;
            partner.Addresses.State = cliente.charge_address_state!;
            partner.Addresses.Country = GetCountryId(cliente.charge_address_country!);
            partner.Addresses.ZipCode = cliente.charge_address_zipcode!;
            partner.Addresses.Block = cliente.charge_address_neighborhood!;
            partner.Addresses.BuildingFloorRoom = cliente.charge_address_complement!;
            partner.Addresses.City = cliente.charge_address_city!;
            partner.Addresses.County = GetCityByNameAndState(partner.Addresses.State, partner.Addresses.City);
            // dados fiscais
            string cnpj = CNPJMask(cliente.cgc!);
            string stateRegistration = "";
            if (string.IsNullOrEmpty(cliente.registration_state) || cliente.registration_state.ToUpper() == "ISENTO")
                stateRegistration = "Isento";
            else
                stateRegistration = cliente.registration_state;
            partner.FiscalTaxID.TaxId0 = cnpj;
            partner.FiscalTaxID.TaxId1 = stateRegistration;
            partner.FiscalTaxID.Address = cliente.charge_address_street!.ToUpper();
            partner.FiscalTaxID.Add();
            partner.FiscalTaxID.TaxId0 = cnpj;
            partner.FiscalTaxID.TaxId1 = stateRegistration;
            partner.UserFields.Fields.Item("U_CadastraF1").Value = "Y";
            partner.UserFields.Fields.Item("U_F1_Id").Value = cliente.id.ToString();

            if (partner.Add() != 0)
                throw new Exception($"Erro ao inserir os dados do parceiro: {DIAPI.API.Company.GetLastErrorDescription()}");

            string key = DIAPI.API.Company.GetNewObjectKey();
            cliente.code = key;
            _logger.LogInformation("cliente {key} adicionado com sucesso atualizando dados", key);
            _ = await _connection.PUTString(url, header, cliente);
            return key;
        }

        private string GetNewCode()
        {
            string query = "SELECT Max(CardCode) FROM OCRD where CardCode like 'C%'";
            Recordset recordset = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            recordset.DoQuery(query);
            if (recordset.RecordCount < 1)
            {
                Marshal.ReleaseComObject(recordset);
                return string.Empty;
            }

            string value = Convert.ToString(recordset.Fields.Item(0).Value);
            value = "C" + (int.Parse(value.Remove(0, 1)) + 1);
            Marshal.ReleaseComObject(recordset);
            return value;
        }

        private void UpdateBP(string code, CRM cliente, string paymentMethod)
        {
            BusinessPartners partner = (BusinessPartners)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oBusinessPartners);
            partner.GetByKey(code);
            bool billExist = false;
            bool shipExist = false;
            for(int index = 0; index < partner.Addresses.Count; index++)
            {
                partner.Addresses.SetCurrentLine(index);
                switch (partner.Addresses.AddressType)
                {
                    case BoAddressType.bo_BillTo:
                        {
                            if (partner.Addresses.ZipCode == cliente.charge_address_zipcode)
                                billExist = true;
                        }
                        break;
                    case BoAddressType.bo_ShipTo:
                        {
                            if (partner.Addresses.ZipCode == cliente.delivery_address_zipcode)
                                shipExist = true;
                        }
                        break;
                }
            }

            if (!billExist)
            {
                partner.Addresses.Add();
                partner.Addresses.AddressName = cliente.charge_address_street!.ToUpper();
                partner.Addresses.AddressType = BoAddressType.bo_BillTo;
                partner.Addresses.Street = cliente.charge_address_street!;
                partner.Addresses.StreetNo = cliente.charge_address_number!;
                partner.Addresses.State = cliente.charge_address_state!;
                partner.Addresses.Country = GetCountryId(cliente.charge_address_country!);
                partner.Addresses.ZipCode = cliente.charge_address_zipcode!;
                partner.Addresses.Block = cliente.charge_address_neighborhood!;
                partner.Addresses.BuildingFloorRoom = cliente.charge_address_complement!;
                partner.Addresses.City = cliente.charge_address_city!;
                partner.Addresses.County = GetCityByNameAndState(partner.Addresses.State, partner.Addresses.City);
            }

            if (!shipExist)
            {
                partner.Addresses.AddressName = cliente.delivery_address_street!.ToUpper();
                partner.Addresses.AddressType = BoAddressType.bo_ShipTo;
                partner.Addresses.Street = cliente.delivery_address_street!;
                partner.Addresses.StreetNo = cliente.delivery_address_number!;
                partner.Addresses.State = cliente.delivery_address_state!;
                partner.Addresses.Country = GetCountryId(cliente.delivery_address_country!);
                partner.Addresses.ZipCode = cliente.delivery_address_zipcode!;
                partner.Addresses.Block = cliente.delivery_address_neighborhood!;
                partner.Addresses.BuildingFloorRoom = cliente.delivery_address_complement!;
                partner.Addresses.City = cliente.delivery_address_city!;
                partner.Addresses.County = GetCityByNameAndState(partner.Addresses.State, partner.Addresses.City);
            }

            if(string.IsNullOrEmpty(cliente.email))
                partner.EmailAddress = cliente.email;
            if(string.IsNullOrEmpty(cliente.phone_number))
                partner.Phone1 = cliente.phone_number;

            //payment methods
            if (!string.IsNullOrEmpty(paymentMethod))
            {
                bool addMethod = true;
                for (int index = 0; index < partner.BPPaymentMethods.Count; index++)
                {
                    partner.BPPaymentMethods.SetCurrentLine(index);
                    if (partner.BPPaymentMethods.PaymentMethodCode == paymentMethod)
                        addMethod = false;
                }

                if (addMethod)
                {
                    partner.BPPaymentMethods.Add();
                    partner.BPPaymentMethods.PaymentMethodCode = paymentMethod;
                }
            }

            if (partner.Update() != 0)
            {
                string erro = DIAPI.API.Company.GetLastErrorDescription();
                _logger.LogError("Erro ao tentar Atualizar o parceiro F1 para o SAP: {erro}", erro);
                if (erro.Contains("RPC_E_SERVERFAULT") || erro.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        private bool Exists(string document, out string code)
        {
            code = "";
            string mask = document.Length == 14 ? CNPJMask(document) : CPFMask(document);
            string query = $"SELECT CardCode from CRD7 WHERE TaxId0 ='{mask}' OR TaxId4 = '{mask}'";
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            recordset.DoQuery(query);
            if(recordset.RecordCount > 0)
            {
                code = (string)recordset.Fields.Item(0).Value;
                return true;
            }

            return false;
        }

        private string CPFMask(string document)
        {
            //012 345 678 90
            //980.320.590-08
            return $"{document.Substring(0, 3)}.{document.Substring(3, 3)}.{document.Substring(6, 3)}-{document.Substring(9, 2)}";
        }

        private string CNPJMask(string document)
        {
            //01 234 567 8901 23
            //29.523.759/0001-30 mask
            return $"{document.Substring(0, 2)}.{document.Substring(2, 3)}.{document.Substring(5, 3)}/{document.Substring(8, 4)}-{document.Substring(12, 2)}";
        }

        private string GetCityByNameAndState(string state, string city)
        {
            string query = $"SELECT AbsId FROM OCNT WHERE NAME  COLLATE Latin1_general_CI_AI Like '{city}' COLLATE Latin1_general_CI_AI AND State = '{state}'";
            Recordset recordset = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            recordset.DoQuery(query);
            if (recordset.RecordCount < 1)
            {
                _logger.LogError("id da cidade {id} não foi encontrada", city);
                return string.Empty;
            }
            return Convert.ToString(recordset.Fields.Item("AbsId").Value);
        }

        private string GetCountryId(string country)
        {
            string query = $"SELECT Code FROM OCRY WHERE NAME = '{country}'";
            Recordset recordset = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            recordset.DoQuery(query);
            if (recordset.RecordCount < 1)
            {
                _logger.LogError("code do pais {id} não foi encontrado", country);
                return string.Empty;
            }
            return Convert.ToString(recordset.Fields.Item(0).Value);
        }

        public async void CadastraSegmentos()
        {
            try
            {
                string query = Queries.Segmentos;
                Recordset recordset = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                recordset.DoQuery(query);
                CRM_Segments? segments = await _connection.GET<CRM_Segments>(Segments_URL, header);
                if (segments is null)
                {
                    segments = new CRM_Segments();
                }

                CRM_Segments newSegments = new();
                bool update = false;
                while (!recordset.EoF)
                {
                    if (!segments.data.Any(x => x.code == (string)recordset.Fields.Item(0).Value))
                    {
                        update = true;
                        newSegments.data.Add(new Segments()
                        {
                            code = (string)recordset.Fields.Item(0).Value,
                            external_code = (string)recordset.Fields.Item(0).Value,
                            external_id = (string)recordset.Fields.Item(0).Value,
                            name = (string)recordset.Fields.Item(1).Value,
                        });
                    }
                    recordset.MoveNext();
                }

                if (update)
                {
                    _ = await _connection.POSTString(Segments_URL, header, newSegments);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar Cadastrar os segmentos {error}", ex.Message);
            }
        }
    }
}