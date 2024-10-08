using SAPbobsCOM;
using ServicoF1.Models.F1.Produtos;
using Caracteristics = ServicoF1.Models.F1.ProductsCaracteristics;
using ServicoF1.Models.WEB;
using System.Runtime.InteropServices;
using ServicoF1.Enums;
using ServicoF1.Models.F1.ProductsCaracteristics;
using System.Text.Json;
using ServicoF1.Models.F1.ProductsCaracteristics.Error;
using ServicoF1.Models.F1.Errors;
using ServicoF1.Uteis;
using System.Globalization;

namespace ServicoF1.servicos
{
    public sealed class ProductsService
    {
        private const string ProductsURL = "https://apidatain-prod.f1ws.com.br/api/products";
        private const string AttributesURL = "https://apidatain-prod.f1ws.com.br/api/products_attributes";
        private const string ClassesURL = "https://apidatain-prod.f1ws.com.br/api/class";
        private const string CategoriesURL = "https://apidatain-prod.f1ws.com.br/api/categories";
        private readonly string _token;
        private readonly bool _homolog;
        private readonly ILogger<Worker> _logger;
        private readonly API.ApiConnection _connection;

        public ProductsService(ILogger<Worker> logger, string token, bool homolog)
        {
            _logger = logger;
            _token = token;
            _homolog = homolog;
            _connection = new API.ApiConnection(_token, _logger);
        }

        /*
         * Ordem de cadatro de produtos
         * atributos
         * classes - só pode haver uma classe sem pai, todas as demais devem ter pais
         * categorias
         * produtos
         */
        public void Run(bool update = false, bool DataSendIntoLogger = false)
        {
            CadastraAtributos(update);
            CadastraClasses(update);
            CadastraCategorias(update);
            if (!update)
                CadastraItems();
            else
                UpdateItemsToF1(DataSendIntoLogger);
        }

        public async void CadastraAtributos(bool update = false)
        {
            try
            {
                ProductsAttributes attributes = GetAtributess(update);
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = AttributesURL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");

                if (update)
                {
                    for (int index = 0; index < attributes.data.Length; index++)
                    {
                        ProductAttributes attribute = attributes.data[index];
                        string uri = $"{url}/{attribute.code}";
                        _ = await _connection.PUT<ProductAttributes, ProductAttributes>(uri, header, attribute);
                    }
                }
                else
                {
                    ProductAttributes[]? atributos = await _connection.POST<ProductsAttributes, ProductAttributes[]>(url, header, attributes);
                    if (atributos is null || atributos.Length < 1)
                        return;

                    UserTable oUserTable = DIAPI.API.Company.UserTables.Item("F1_ATRIBUTOS");
                    foreach (var attribute in atributos)
                    {
                        string code = attributes.data!.First(x => x.code == attribute.code).SapCode!;
                        oUserTable.GetByKey(code);
                        oUserTable.UserFields.Fields.Item("U_F1_Id").Value = attribute.id.ToString();
                        if (oUserTable.Update() != 0)
                        {
                            string erro = DIAPI.API.Company.GetLastErrorDescription();
                            _logger.LogError("Erro ao atualizar o id do F1 para o atributo {atributo}| erro {erro}", attribute.code, erro);
                            if (erro.Contains("RPC_E_SERVERFAULT") || erro.Contains("SLD"))
                            {
                                DIAPI.API.Reset = true;
                            }
                        }
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Marshal.ReleaseComObject(oUserTable);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao enviar os atributos pra cadastro F1 | erro {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        public static ProductsAttributes GetAtributess(bool update)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            string query;
            if (update)
                query = "SELECT \"U_Identificador\", \"Name\", \"U_Type\", \"Code\" FROM \"@F1_ATRIBUTOS\" WHERE ISNULL(\"U_F1_Id\", '') <> '' AND \"U_F1_Id\" <> 'mock'";
            else
                query = "SELECT \"U_Identificador\", \"Name\", \"U_Type\", \"Code\" FROM \"@F1_ATRIBUTOS\" WHERE ISNULL(\"U_F1_Id\", '') = ''";
            recordset.DoQuery(query);
            if (recordset.RecordCount < 1)
                return new ProductsAttributes(0);

            ProductsAttributes attributes = new(recordset.RecordCount);
            int index = 0;
            while (!recordset.EoF)
            {

                attributes.data[index] = new Caracteristics.ProductAttributes
                {
                    code = Convert.ToString(recordset.Fields.Item(0).Value).Replace("/", "_").Replace("\\", "_"),
                    name = Convert.ToString(recordset.Fields.Item(1).Value),
                    type = Convert.ToString(recordset.Fields.Item(2).Value),
                    SapCode = ((int)recordset.Fields.Item(3).Value).ToString()!
                };

                if (attributes.data[index].type == "inteiro")
                    attributes.data[index].element_type = "Checkbox";

                recordset.MoveNext();
                index++;
            }


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Marshal.ReleaseComObject(recordset);
            }

            return attributes;
        }

        // tabela de classes alterada para "@SX_SUBGRUPO"
        public async void CadastraClasses(bool atualizar = false)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            try
            {
                // workflows mais correto seria talvez cadastrar o produtos e depois cadastrar os preços dem separado pois parece haver mais opções mas isso deve ser melhor explorado
                var classes = GetClases(recordset, atualizar);
                if (classes.data.Count < 1)
                    return;

                Header[] header = new Header[1] { new Header("Bearer", _token) };

                if (!atualizar)
                {
                    _logger.LogInformation("Cadastrando {qtd} Classes", classes.data.Count);
                    string url = ClassesURL;
                    if (_homolog)
                        url = url.Replace("apidatain-prod", "apidatain-homolog");
                    Caracteristics.ClassResponse[]? classesResponse = await _connection.POST<Caracteristics.ProductClasses, Caracteristics.ClassResponse[]>(url, header, classes, true);
                    if(classesResponse is null)
                    {
                        _logger.LogError("Erro ao recuperar o json de retorno do cadastro de classes a respota foi nula");
                        return;
                    }

                    foreach (var @class in classesResponse)
                    {
                        string code = @class.code;
                        string id = @class.id.ToString();
                        recordset.DoQuery($"Update \"@SX_SUBGRUPO\" set \"U_classe_F1_Id\" = '{id}' WHERE \"Code\" = '{code}'");
                    }
                }
                else
                {
                    _logger.LogInformation("Atualizando {qtd} Classes", classes.data.Count);
                    string url = ClassesURL;
                    if (_homolog)
                        url = url.Replace("apidatain-prod", "apidatain-homolog");

                    for(int index = 0; index < classes.data.Count; index++)
                    {
                        var @class = classes.data[index];
                        await UpdateClass(@class, header, url);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao enviar as Classe pra cadastro F1 | erro {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                // tentar buscar o id da classe já cadastraada e atualizar no SAP
                else if (ex.Message.Contains("Duplicate entry") && !atualizar)
                {
                    FixClassesWronglyAdded(ex.Message, recordset);
                }
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && recordset is not null)
                    Marshal.ReleaseComObject(recordset);
            }
        }

        private async void FixClassesWronglyAdded(string message, Recordset recordset)
        {
            try
            {
                _logger.LogInformation("Tentando recadastrar o ID para as classes");
                // sempre sera o segundo conferme os erros feitos nos envios HTTP
                string value = message.Split("|")[1];
                value = value.Trim();
                Error[] errors = JsonSerializer.Deserialize<Error[]>(value)!;
                ObjectWithError[] repeated = JsonSerializer.Deserialize<ObjectWithError[]>(value)!;
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = ClassesURL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");
                foreach (Error error in errors)
                {
                    if (string.IsNullOrEmpty(error.exception_message))
                        continue;
                    //get the inicial index of the starting index of the cuplicate entry code
                    // why the fuck they dont return the entry in another field GOD
                    string entry = GetEntry(error.exception_message);
                    // corrige categorias que foram deletadas no SAP mas ainda existem no RD então o ID delas e salvo no SAP 
                    var temp = await _connection.GET<ClassResponse>(url + $"/{entry}", header);
                    if (temp != null)
                        recordset.DoQuery($"Update \"@SX_SUBGRUPO\" set \"U_classe_F1_Id\" = '{temp.id}' WHERE \"Code\" = '{temp.code}'");
                }

                foreach(ObjectWithError repeat in repeated)
                {
                    if (string.IsNullOrEmpty(repeat.code))
                        continue;
                    recordset.DoQuery($"Update \"@SX_SUBGRUPO\" set \"U_classe_F1_Id\" = '{repeat.id}' WHERE \"Code\" = '{repeat.code}'");
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao tentar corrigir as classes: {error}", ex.Message);
            }
        }

        private static string GetEntry(string error)
        {
            ReadOnlySpan<char> span = error.AsSpan();
            int startIndex = span.IndexOf('\'');
            string entry = string.Empty;
            for (int index = startIndex + 1; index < span.Length; index++) 
            {
                char value  = span[index];
                if (value == '\'')
                    break;
                entry += value;
            }

            return entry;
        }

        private async Task UpdateClass(Class @class, Header[] header, string url)
        {
            try
            {
                _ = await _connection.PUT<Class, ClassResponse>(url + $"/{@class.code}", header, @class);
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao Atualizar a classe {class} | erro: {error}", @class.code, ex.Message);
            }
        }

        // tabela de classes alterada para "@SX_SUBGRUPO"
        private ProductClasses GetClases(Recordset recordset, bool atualizar)
        {

            //string query = "Select T0.\"Code\", ISNULL(T1.\"Name\",''), \"U_Desc\", T2.\"U_Identificador\" From \"@F1_ATRIBUTOS_CLASES\" T0"
            //+ " Inner Join \"@SX_SUBGRUPO\" T1 On T1.\"Code\" = T0.\"Code\""
            //+ " Inner Join \"@F1_ATRIBUTOS\" T2 ON T0.\"U_Atr_Code\" = T2.\"Code\""
            //+ " WHERE ISNULL(T1.\"U_classe_F1_Id\", '') = '' Order By T0.\"Code\"";

            string query;
            if (atualizar)
                query = "SELECT \"Code\", \"Name\" FROM \"@SX_SUBGRUPO\" WHERE ISNULL(\"U_classe_F1_Id\", '') <> '' AND \"U_classe_F1_Id\" <> 'mock' Order By \"Code\"";
            else
                query = "SELECT \"Code\", \"Name\" FROM \"@SX_SUBGRUPO\" WHERE ISNULL(\"U_classe_F1_Id\", '') = '' Order By \"Code\"";
            recordset.DoQuery(query);
            if (recordset.RecordCount < 1)
                return new ProductClasses();

            string pai = BuscaClassePai();
            // a api da f1 aceita somente uma classe sem pai todas as demais
            // devem ser filhas de uma classe
            if (string.IsNullOrEmpty(pai))
                throw new Exception("Nenhuma classe esta marcada como pai");

            ProductClasses productClasses = new();
            while (!recordset.EoF)
            {
                //if (lastCode != Convert.ToString(recordset.Fields.Item(0).Value)
                //{
                //    productClasses.data.Add(new Caracteristics.Class());
                //    index++;
                //}
                Class classe = new()
                {
                    code = Convert.ToString(recordset.Fields.Item(0).Value)
                };
                if (classe.code != pai)
                    classe.parent_class = pai;

                classe.name = Convert.ToString(recordset.Fields.Item(1).Value);
                classe.attributes = GetClassAtributtes(classe.code, pai);
                productClasses.data.Add(classe);
                recordset.MoveNext();
            }

            return productClasses;
        }

        private List<Caracteristics.Attribute> GetClassAtributtes(string classID, string pai)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            try
            {
                string query = $"Select T2.\"U_Identificador\" From \"@F1_ATRIBUTOS_CLASES\" T0\r\n            Inner Join \"@F1_ATRIBUTOS\" T2 ON T0.\"U_Atr_Code\" = T2.\"Code\"\r\n            WHERE T0.Code IN ('{pai}', '{classID}')";
                recordset.DoQuery(query);
                List<Caracteristics.Attribute> lista = new();
                while(!recordset.EoF)
                {
                    string code = Convert.ToString(recordset.Fields.Item(0).Value).Replace("/", "_").Replace("\\", "_");
                    lista.Add(new Caracteristics.Attribute(code, "dados"));
                    recordset.MoveNext();
                }

                return lista;
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao buscar os atributos da classe {classe} | erro {erro}", classID, ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return new List<Caracteristics.Attribute>();
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }
        }

        private static string BuscaClassePai()
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            try
            {
                string query = "select Code from \"@SX_SUBGRUPO\" where U_Classe_Pai = 'Y'";
                recordset.DoQuery(query);
                if (recordset.RecordCount > 0)
                    return Convert.ToString(recordset.Fields.Item(0).Value);
            }
            catch { }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }

            return string.Empty;
        }

        private Category[] GetCategoriasItem(string categoryCode)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            try
            {
                Category[] categories = GetCategoriasItem(categoryCode, 1, recordset);
                return categories;
            }
            catch
            {
                return Array.Empty<Category>();
            }
            finally
            {
                if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }
        }

        private Category[] GetCategoriasItem(string categoryCode, int repetition, Recordset recordset)
        {
            switch (repetition)
            {
                case 1 :
                    {
                        string query = $"SELECT T0.Code, T1.ItmsGrpCod, T2.Code from \"@SX_GGRUPO\" T0 INNER JOIN \"OITB\" T1 ON T1.U_Parent = T0.Code INNER JOIN \"@SX_SUBGRUPO\" T2 ON T2.U_parent = T1.ItmsGrpCod WHERE T2.Code = '{categoryCode}'";
                        recordset.DoQuery(query);
                        Category[] categories = new Category[3];
                        if (recordset.RecordCount > 0)
                        {
                            recordset.MoveFirst();
                            categories[0] = new Category
                            {
                                code = Convert.ToString(recordset.Fields.Item(0).Value)
                            };

                            categories[1] = new Category
                            {
                                code = Convert.ToString(recordset.Fields.Item(1).Value)
                            };

                            categories[2] = new Category
                            {
                                code = Convert.ToString(recordset.Fields.Item(2).Value)
                            };
                            return categories;
                        }

                        return GetCategoriasItem(categoryCode, repetition + 1, recordset);
                    }
                case 2:
                    {
                        string query = $"SELECT T0.ItmsGrpCod, T1.Code from \"OITB\" T0 INNER JOIN \"@SX_SUBGRUPO\" T1 ON T1.U_parent = T0.ItmsGrpCod WHERE T1.Code = '{categoryCode}'";
                        recordset.DoQuery(query);
                        Category[] categories = new Category[2];
                        if (recordset.RecordCount > 0)
                        {
                            recordset.MoveFirst();
                            categories[0] = new Category
                            {
                                code = Convert.ToString(recordset.Fields.Item(0).Value)
                            };

                            categories[1] = new Category
                            {
                                code = Convert.ToString(recordset.Fields.Item(1).Value)
                            };
                            return categories;
                        }

                        return GetCategoriasItem(categoryCode, repetition + 1, recordset);
                    }
                case 3:
                    {
                        string query = $"SELECT T0.Code, T1.Code from \"@SX_GGRUPO\" T0 INNER JOIN \"@SX_SUBGRUPO\" T1 ON T1.U_parent = T0.Code WHERE T1.Code = '{categoryCode}'";
                        recordset.DoQuery(query);
                        Category[] categories = new Category[2];
                        if (recordset.RecordCount > 0)
                        {
                            recordset.MoveFirst();
                            categories[0] = new Category
                            {
                                code = Convert.ToString(recordset.Fields.Item(0).Value)
                            };

                            categories[1] = new Category
                            {
                                code = Convert.ToString(recordset.Fields.Item(1).Value)
                            };
                            return categories;
                        }

                        return GetCategoriasItem(categoryCode, repetition + 1, recordset);
                    }
                default:
                    {
                        return Array.Empty<Category>();
                    }
            }
        }

        /*
         * Cadasstro de categorias mudou segundo consulta com João dressen da neoyama
         * cadastro de level 1 são os itens da tabela SX_GGRUPOS
         * cadastro de level 2 são os grupos de item padrão do SAP
         * cadastro de level 3 são os itens da tabela SX_SUBGRUPO que também vai servir de classe usando uma tabela auxiliar
         */
        public async void CadastraCategorias(bool atualizar = false)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            int nivel = 1;
            try
            {
                // workflows mais correto seria talvez cadastrar o produtos e depois cadastrar os preços dem separado pois parece haver mais opções mas isso deve ser melhor explorado
                // new query esperando confirmação da neoyama para mudança
                /*
                 * SELECT \"Code\", \"Name\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM \"@SX_GGRUPO\" WHERE ISNULL(\"U_F1_Id\", '') = '' nivel 1
                 * SELECT \"ItmsGrpCod\", \"ItmsGrpNam\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM OITB WHERE ISNULL(\"U_F1_Id\", '') = '' nivel 2
                 * SELECT \"Code\", \"Name\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM \"@SX_SUBGRUPO\" WHERE ISNULL(\"U_F1_Id\", '') = '' nivel 3
                 */
                string query = String.Empty;
                string table = String.Empty;
                string field = String.Empty;
                for (; nivel <= 3; nivel++)
                {
                    switch (nivel)
                    {
                        case 1: 
                            {
#if DEBUG
                                query = "SELECT \"Code\", \"Name\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM \"@SX_GGRUPO\" WHERE ISNULL(\"U_F1_Id\", '') = ''";
#endif
#if RELEASE || RELEASE_PRO
                                if(atualizar)
                                    query = "SELECT \"Code\", \"Name\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM \"@SX_GGRUPO\" WHERE ISNULL(\"U_F1_Id\", '') <> '' AND \"U_F1_Id\" <> 'mock'";
                                else
                                    query = "SELECT \"Code\", \"Name\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM \"@SX_GGRUPO\" WHERE ISNULL(\"U_F1_Id\", '') = ''";
#endif
                                table = "@SX_GGRUPO";
                                field = "Code";
                            }
                            break;
                        case 2:
                            {
#if DEBUG
                                query = "SELECT top 1 \"ItmsGrpCod\", \"U_U_SX_DETALHADO\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM OITB WHERE ISNULL(\"U_F1_Id\", '') = '' AND ISNULL(\"U_Parent\", '') <> ''";
#endif
#if RELEASE || RELEASE_PRO
                                if (atualizar)
                                    query = "SELECT \"ItmsGrpCod\", \"U_U_SX_DETALHADO\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM OITB WHERE ISNULL(\"U_F1_Id\", '') <> ''  AND ISNULL(\"U_Parent\", '') <> '' AND \"U_F1_Id\" <> 'mock'";
                                else
                                    query = "SELECT \"ItmsGrpCod\", \"U_U_SX_DETALHADO\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM OITB WHERE ISNULL(\"U_F1_Id\", '') = ''  AND ISNULL(\"U_Parent\", '') <> ''";
#endif
                                table = "OITB";
                                field = "ItmsGrpCod";
                            }
                            break;
                        case 3:
                            {
#if DEBUG
                                query = "SELECT top 1 \"Code\", \"Name\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM \"@SX_SUBGRUPO\" WHERE ISNULL(\"U_F1_Id\", '') = '' AND ISNULL(\"U_Parent\", '') <> ''";
#endif
#if RELEASE || RELEASE_PRO
                                if (atualizar)
                                    query = "SELECT \"Code\", \"Name\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM \"@SX_SUBGRUPO\" WHERE ISNULL(\"U_F1_Id\", '') <> ''  AND ISNULL(\"U_Parent\", '') <> '' AND \"U_F1_Id\" <> 'mock'";
                                else
                                    query = "SELECT \"Code\", \"Name\", \"U_Discount_Max\", \"U_Discount_Fix\", \"U_Parent\" FROM \"@SX_SUBGRUPO\" WHERE ISNULL(\"U_F1_Id\", '') = ''  AND ISNULL(\"U_Parent\", '') <> ''";
#endif
                                table = "@SX_SUBGRUPO";
                                field = "Code";
                            }
                            break;
                    }


                    recordset.DoQuery(query);
                    Caracteristics.Categorias categorias = new(recordset.RecordCount);
                    int index = 0;
                    while (!recordset.EoF)
                    {
                        Caracteristics.Categoria category = new()
                        {
                            active = 1,
                            code = Convert.ToString(recordset.Fields.Item(0).Value),
                            name = Convert.ToString(recordset.Fields.Item(1).Value),
                            level = nivel,
                            discount_max = (int)recordset.Fields.Item(2).Value,
                            discount_fix = (int)recordset.Fields.Item(3).Value,
                            order = 1,
                            parent_category = Convert.ToString(recordset.Fields.Item(4).Value),
                        };

                        categorias.data[index] = category;
                        recordset.MoveNext();
                        index++;
                    }

                    if (categorias.data.Length < 1)
                        continue;
                    string url = CategoriesURL;
                    if (_homolog)
                        url = url.Replace("apidatain-prod", "apidatain-homolog");
                    Header[] header = new Header[1] { new Header("Bearer", _token) };

                    if (atualizar)
                    {
                        _logger.LogInformation("Atualizando {qtd} categorias", categorias.data.Length);

                        for (int i = 0; i < categorias.data.Length; i++)
                        {
                            await UpdateCategory(recordset, table, field, categorias.data[i], url, header);
                        }

                        continue;
                    }

                    _logger.LogInformation("Cadastrando {qtd} categorias", categorias.data.Length);
                    CategoryResponse[]? categorias1 = await _connection.POST<Categorias, CategoryResponse[]>(url, header, categorias);
                    if(categorias1 is null)
                    {
                        _logger.LogInformation("Erro ao tentar cadastrar as categorias de nivel {nvl}, retorno nulo do json", nivel);
                        continue;
                    }

                    List<Categoria> data = categorias.data.ToList();
                    foreach (CategoryResponse categoria in categorias1)
                    {
                        recordset.DoQuery($"Update \"{table}\" set \"U_F1_Id\" = '{categoria.id}' WHERE \"{field}\" = '{categoria.code}'");
                        data.RemoveAll(x => x.code == categoria.code);
                    }

                    if(data.Count > 0)
                    {
                        _logger.LogInformation("Tentando recadastrar o ID para categorias que já estão cadastradas qtd: {qtd}", data.Count);
                        foreach (Categoria categoria in data)
                        {
                            // corrige categorias que foram deletadas no SAP mas ainda existem no RD então o ID delas e salvo no SAP 
                            var temp = await _connection.GET<CategoryResponse>(url + $"/{categoria.code}", header);
                            if(temp != null)
                                recordset.DoQuery($"Update \"{table}\" set \"U_F1_Id\" = '{temp.id}' WHERE \"{field}\" = '{categoria.code}'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao enviar os categorias pra cadastro F1 no nivel {nivel} | erro {erro}", nivel, ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }

                // TODO: Criar  uma maneira de investigar caso a Categoria já tenha sido cadastrada caso tenha sido eu devo aidicionar o ID com base no nivel
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }
        }

        private async Task UpdateCategory(Recordset recordset, string table, string field, Categoria categoria, string url, Header[] header)
        {
            try
            {
                CategoryResponse? temp_cat = await _connection.PUT<Categoria, CategoryResponse>(url + $"/{categoria.code}", header, categoria);
                if(temp_cat is null)
                {
                    _logger.LogError("Erro ao Atualizar a Categoria {cat} json de update retornou nulo", categoria.code);
                    return;
                }

                recordset.DoQuery($"Update \"{table}\" set \"U_F1_Id\" = '{temp_cat.id}' WHERE \"{field}\" = '{temp_cat.code}'");
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao Atualizar a Categoria {cat}| erro: {error}", categoria.code, ex.Message);
            }
        }

        public async void CadastraItems()
        {
            Items items = (Items)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oItems);
            try
            {
                Products products = GetItems(QueryType.CreateItem);
                if (products.data.Length < 1)
                    return;
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = ProductsURL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");
                _logger.LogInformation("Tentando cadastrar {num} produtos", products.data.Length);
                Response[]? response = await _connection.POST<Products, Response[]>(url, header, products, true);
                if (response is null || response.Length < 0)
                    return;

                for (int index = 0; index < response.Length; index++)
                {
                    UpdateSapItem(response[index].code, response[index].id, items);
                }
            }
            catch (Exception ex)
            {
                if(ex.Message.Contains("Duplicate entry"))
                {
                    // tenta corrigir os produtos com erro de um erro gerado pela api connection o erro é subdivido usando o char | e na posição 1 esta a string de resposta da API com as chaves
                    // duplicadas
                    string Duplicate_Produtcst_json = ex.Message.Split("|")[1];
                    CorrectProducts(Duplicate_Produtcst_json, items);
                }

                _logger.LogError("Erro ao enviar os produtos pra cadastro F1 | erro {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(items);
            }
        }

        public void CorrectProducts(string duplicate_Produtcst_json, Items items)
        {
            try
            {
                if (string.IsNullOrEmpty(duplicate_Produtcst_json))
                    return;

                F1_Error[]? errors = JsonSerializer.Deserialize<F1_Error[]>(duplicate_Produtcst_json);
                if (errors is null)
                    return;

                _logger.LogInformation("Corrigindo items");
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = ProductsURL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");
                for (int index = 0; index < errors.Length; index++)
                {
                    // busca o code do item
                    ReadOnlySpan<char> valores = errors[index].exception_message.AsSpan();
                    int start = valores.IndexOf('\'');
                    string code = "";
                    for(int i = start + 1; i < valores.Length; i++)
                    {
                        if (valores[i].Equals('\''))
                            break;
                        code += valores[i];
                    }
                    _logger.LogInformation("Corrigindo item {item}", code);
                    // busca o id do item já pre cadastrado
                    Response? response = _connection.GET<Response>(url + $"/{code}", header).Result;
                    if(response != null)
                        UpdateSapItem(response.code, response.id, items);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar corrigir os items com codes duplicados F1 | erro {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        public async void UpdateItemsToF1(bool DataSendIntoLogger)
        {
            Items items = (Items)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oItems);
            try
            {
                Product[] products = GetItems(QueryType.UpdateItem).data;
                if (products.Length < 1)
                    return;

                Header[] header = new Header[1] { new Header("Bearer", _token) };
                string url = ProductsURL;
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");

                _logger.LogInformation("Atualizando {count} produtos", products.Length);
                for(int index = 0; index < products.Length; index++)
                {
                    try
                    {
                        string uri = $"{url}/{products[index].code}";
                        // the await may be causing a concurrence error
                        var response = UpdateItem(products[index], uri, header, DataSendIntoLogger).GetAwaiter().GetResult();

                        if (response.Length > 0)
                        {
                            if (items.GetByKey(response[0].code))
                            {
                                items.UserFields.Fields.Item("U_F1_Id").Value = response[0].id.ToString();
                                items.Update();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Erro ao atualizar o produto {item} para F1 | erro {erro}", products[index].code, ex.Message);
                        if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                        {
                            DIAPI.API.Reset = true;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao atualizar os produtos para F1 | erro {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && items is not null)
                    Marshal.FinalReleaseComObject(items);
            }
        }

        private async Task<Response[]> UpdateItem(Product product, string url, Header[] header, bool DataSendIntoLogger)
        {
            if (DataSendIntoLogger)
                _logger.LogInformation("Item {item}: data {data}", product.code, JsonSerializer.Serialize(product));

            Response[]? response = await _connection.PUT<Product, Response[]>(url, header, product);
            if (response is null || response.Length < 0)
                return Array.Empty<Response>();

            return response;
        }

        private void UpdateSapItem(string code, int id, Items items)
        {
            if (items.GetByKey(code))
            {
                items.UserFields.Fields.Item("U_F1_Id").Value = id.ToString();
                if (items.Update() != 0)
                {
                    string erro = DIAPI.API.Company.GetLastErrorDescription();
                    _logger.LogError("Erro ao fazer a atualização do item {item} , com id F1 {id} | Erro : {erro}", code, id, erro);
                    if (erro.Contains("RPC_E_SERVERFAULT") || erro.Contains("SLD"))
                    {
                        DIAPI.API.Reset = true;
                    }
                }
            }
        }

        private Products GetItems(QueryType type)
        {
            string query = string.Empty;

            switch (type)
            {
                // 7/10/2022 - alteração do campo U_SX_XubGrupo para comparar com o code e não o nome, mudança feito pelo SAP da neoyama
                case QueryType.CreateItem:
                    query = @"SELECT 
                            T0.""ItemCode"", --0
                            T0.""U_nome_produto"", ---1
                            Case when isnull(T1.""Code"", '') = ''
                            Then
                             (Select ""Code"" FROM ""@SX_SUBGRUPO"" WHERE U_Classe_Pai = 'Y')
                            Else
	                            T1.""Code""
                            end AS 'Code', --2
                            T0.""LeadTime"", --3
                            T3.""GroupName"",  --4
                            T0.""U_F1_Id"", --5
                            T0.""ProductSrc"", --6
                            T0.SLength1, --7
                            T0.SWidth1, --8
                            T0.SHeight1, --9
                            T0.SVolume, --10
                            T0.SWeight1, --11
                            T2.NcmCode,--12
                            T3.GroupCode, --13
                            T4.Name, --14
                            T0.U_CadastrarF1, --15
                            T0.MatType, --16
                            case when  T0.OrdrMulti = 0
							then 
								1
							else
								T0.OrdrMulti
							END AS  'OrdrMulti', --17
                            ISNULL(T0.U_Venda_Sem_Estoque, '1') AS 'Venda_Sem_Estoque', --18
                            T1.U_status -- 19
                            FROM OITM T0  
                            LEFT JOIN ""@SX_SUBGRUPO"" T1 ON T0.""U_SX_SubGrupo"" = T1.""Code"" 
                            LEFT JOIN ONCM T2 ON T2.AbsEntry = T0.NCMCode 
                            LEFT JOIN ONCG T3 ON T2.""Group"" = T3.""GroupCode"" 
                            LEFT JOIN ""@SX_MARCAS"" T4 ON T4.Code = T0.U_marca
                            WHERE ""U_CadastrarF1"" = 'Y' AND ISNULL(T0.""U_F1_Id"",'') = ''";
                    break;
                case QueryType.UpdateItem:
                    query = @"SELECT 
                            T0.ItemCode, --0
                            T0.U_nome_produto, --1
                            Case when isnull(T1.Code, '') = ''
                            Then
                             (Select Code FROM ""@SX_SUBGRUPO"" WHERE U_Classe_Pai = 'Y')
                            Else
	                            T1.Code
                            end AS 'Code', --2
                            T0.LeadTime, --3
                            T3.GroupName, --4
                            T0.U_F1_Id, --5
                            T0.ProductSrc, --6
                            T0.SLength1, --7
                            T0.SWidth1, --8
                            T0.SHeight1, --9
                            T0.SVolume, --10
                            T0.SWeight1, --11
                            T2.NcmCode, --12
                            T3.GroupCode, --13
                            T4.Name, --14
                            T0.U_CadastrarF1, -- 15
                            T0.MatType, --16
                            case when  T0.OrdrMulti = 0
							then 
								1
							else
								T0.OrdrMulti
							END AS  'OrdrMulti', --17
                            ISNULL(T0.U_Venda_Sem_Estoque, '1') AS 'Venda_Sem_Estoque', --18
                            T1.U_status -- 19
                            FROM OITM T0 
                            LEFT JOIN ""@SX_SUBGRUPO"" T1 ON T0.U_SX_SubGrupo = T1.Code
                            LEFT JOIN ONCM T2 ON T2.AbsEntry = T0.NCMCode 
                            LEFT JOIN ONCG T3 ON T2.""Group"" = T3.GroupCode
                            LEFT JOIN ""@SX_MARCAS"" T4 ON T4.Code = T0.U_marca
                            WHERE ""U_CadastrarF1"" = 'Y' AND ISNULL(T0.U_F1_Id,'') <> ''";
                    break;
            }

            return GetItems(query);
        }

        private Products GetItems(string query)
        {
            string lastProduct = "";
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            try
            {
                recordset.DoQuery(query);
                Products products = new(recordset.RecordCount);
                int index = 0;
                while (!recordset.EoF)
                {
                    lastProduct = recordset.Fields.Item(0).Value;
                    if (string.IsNullOrEmpty(Convert.ToString(recordset.Fields.Item(13).Value)))
                    {
                        _logger.LogError("Item {produto} Com GRUPO DE NCM VAZIO!", lastProduct);
                        recordset.MoveNext();
                        continue;
                    }

                    if (!((string)recordset.Fields.Item(19).Value).Equals("A", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("Item {produto} com Classe {class} inativa!", lastProduct, (string)recordset.Fields.Item(2).Value);
                        recordset.MoveNext();
                        continue;
                    }

                    string ativo = Convert.ToString(recordset.Fields.Item(15).Value) == "Y" ? "1" : "0";
                    string ipi = Convert.ToString(recordset.Fields.Item(4).Value);
                    string origem = Convert.ToString(recordset.Fields.Item(6).Value);
                    if (string.IsNullOrEmpty(ipi))
                        ipi = "0.0";
                    else
                    {
                        int i = ipi.IndexOf("%");
                        if (i >= 0)
                        {
                            ipi = ipi[..i];
                        }

                        ipi = ipi.Replace("IPI", "").Replace(',', '.').Trim();
                    }
                    // gerar uma verificação se o item for de determinadas origens e tipo de material mudar o IPI para 0
                    if (Convert.ToString(recordset.Fields.Item(16).Value) == "0" && (origem != "1" && origem != "6"))
                        ipi = "0.0";

                    Product product = new()
                    {
                        code = Convert.ToString(recordset.Fields.Item(0).Value),
                        name = Convert.ToString(recordset.Fields.Item(1).Value),
                        _class = Convert.ToString(recordset.Fields.Item(2).Value),
                        ressuply_deadline = Convert.ToString(recordset.Fields.Item(3).Value),
                        ipi = ipi,
                        F1_Id = Convert.ToString(recordset.Fields.Item(5).Value),
                        active = ativo,
                        multiple_inventory = Convert.ToString(recordset.Fields.Item(17).Value),
                        situation = Convert.ToString(recordset.Fields.Item(18).Value)//ativo == "1" ? "ACTIVE" : "INACTIVE"
                    };

                    product.stock = GetItemStock(lastProduct, product.ressuply_deadline);
                    product.prices = GetItemPriceList(product.code);
                    // substituição do metodo para utilizar a classe como categoria
                    /*
                     * dados adicionauis para os campos de atributos obrigatórios
                     * 6 - origem
                     * 7 - profundidade
                     * 8 - largura
                     * 9 - Altura
                     * 10 - volume
                     * 11 - peso/peso Cúbico
                     * 12 - NCM
                     * 13 - NCM GROUP CODE
                     * 14 - Marca
                     * 15 - CadastraF1
                    */
                    string[] atributosImbutidos = new string[11];
                    // os dois ultimos atributos são o IPI
                    for (int attr = 0; attr < 9; attr++)
                    {
                        switch(attr)
                        {
                            case 1:
                            case 2:
                            case 3:
                                {
                                    string val = Convert.ToString(recordset.Fields.Item(attr + 6).Value);
                                    //_logger.LogInformation("val {val}", val);
                                    double value = Convert.ToDouble(recordset.Fields.Item(attr + 6).Value, new CultureInfo("pt-BR"));
                                    atributosImbutidos[attr] = Convert.ToString(value * 100);
                                }
                                break;
                            default:
                                {
                                    atributosImbutidos[attr] = Convert.ToString(recordset.Fields.Item(attr + 6).Value);
                                }
                                break;
                        }
                    }

                    //ipi
                    atributosImbutidos[9] = ipi.Trim();
                    // verifica se tributa o IPI
                    atributosImbutidos[10] = Convert.ToDouble(ipi) > 0 ? "1" : "0";//string.IsNullOrEmpty(atributosImbutidos[8]) ? "0" : "1";
                    //new Category[] { new Category() { code = "IMPRESSORAS" } };//
                    product.categories = GetCategoriasItem(recordset.Fields.Item(2).Value);
                    product.attributes = GetItemAtributes(product._class, product.code, atributosImbutidos);
                    products.data[index] = product;
                    recordset.MoveNext();
                    index++;
                }
               

                return new Products(products.data.Where(x => x is not null).ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao criar os produtos para serem enviados | produto com erro {produto} | erro {erro}", lastProduct, ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return new Products(0);
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }
        }

        private string GetItemIPI(string ItemCode)
        {
            try
            {
                // this query only works for the production base
                string query_Tax = $@" select tcd5.TaxCode, tcd2.KeyFld_1_V, keyfld_2_v, keyfld_3_v, keyfld_4_v, keyfld_5_v

                              from tcd2

                              inner join tcd3 on tcd3.tcd2id = tcd2.absid

                              inner join tcd5 on tcd5.tcd3id = tcd3.absid

                              where tcd2.tcd1id = 1

                              and isnull(tcd2.KeyFld_1_V,'') in (SELECT CardCode FROM OCRD WHERE CardType = 'C')

                              and isnull(tcd2.keyfld_2_v,'') = '{ItemCode}'

                              and isnull(tcd2.keyfld_3_v,'') = ''

                              and isnull(tcd2.keyfld_4_v,'') = ''

                              and isnull(tcd2.keyfld_5_v,'') = ''

                              --and tcd5.usageCode = ''

                              and isnull(tcd3.EfctTo,999999) >= getdate()

                              and tcd3.EfctFrom <= getdate()

                              order by tcd3.EfctFrom desc";
                Recordset recordset = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                recordset.DoQuery(query_Tax);
                if (recordset.RecordCount < 1)
                    return string.Empty;
                string tax = recordset.Fields.Item(0).Value;
                string query_IPI = $@"select 
                                ostc.Code as codigo, 
                                ostc.Name as descricao, OSTA.Name,STC1.*,osta.SalesTax,

                                osta.PurchTax, OSTA.U_BASE, OSTA.U_ISENTO, OSTA.U_Outros, STA1.Rate,

                                OFML.Code, osta.NonDdctPrc, osta.NonDdctact, STA1.U_LUCRO ,STA1.U_REDUCAO1, STA1.U_REDUCAO2,STA1.U_REDUICMS,ostc.tfcid

                                from STC1

                                left join osta on (OSTA.code     = STC1.STACode)

                                left join STA1 on (STA1.StaCode = OSTA.Code)

                                left join OFML on (OFML.AbsId = STC1.FmlId)

                                left join ostc on (ostc.Code       = STC1.STCCode)

                                left join ostt on (ostt.AbsId = stc1.STAType )

                                where STCCode = '{tax}' and osta.Type     = STC1.STAType and STA1.SttType = STC1.STAType
                                and ostt.NfTaxId = -4
                                order by sta1.EfctDate";
                recordset.DoQuery(query_IPI);
                return Convert.ToString(recordset.Fields.Item("EfctivRate").Value);
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao tentar recuperar o ipi do item {item}| erro: {erro}", ItemCode, ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return "";
            }
        }

        private static Attribute1[] GetItemAtributes(string _class, string code,string[] atributosImbutidos)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            try
            {
                string query = $"SELECT distinct T1.\"U_Identificador\" FROM \"@F1_ATRIBUTOS\" T1 inner join (SELECT T2.\"U_Atr_Code\", '' AS U_Value FROM \"@F1_ATRIBUTOS_CLASES\" T2 WHERE T2.\"Code\" = '{_class}' OR T2.\"Code\" = (select T3.\"Code\" FROM \"@SX_SUBGRUPO\" T3 WHERE T3.U_Classe_Pai = 'Y')) TAB ON TAB.U_Atr_Code = T1.Code";
                recordset.DoQuery(query);
                Dictionary<string, Attribute1> attributes = new();
                int index = 0;
                while (!recordset.EoF)
                {
                    string atributo = Convert.ToString(recordset.Fields.Item(0).Value).Replace("/", "_").Replace("\\", "_");
                    string value = "";
                    switch (atributo.ToLower())
                    {
                        case "origem":
                            {
                                value = atributosImbutidos[0];
                            }
                            break;
                        case "profundidade":
                            {
                                value = atributosImbutidos[1].Replace(",", ".");
                            }
                            break;
                        case "largura":
                            {
                                value = atributosImbutidos[2].Replace(",", ".");
                            }
                            break;
                        case "tamanho":
                            {
                                value = atributosImbutidos[2].Replace(",", ".");
                            }
                            break;
                        case "altura":
                            {
                                value = atributosImbutidos[3].Replace(",", ".");
                            }
                            break;
                        case "volume":
                            {
                                value = atributosImbutidos[4].Replace(",", ".");
                            }
                            break;
                        case "peso":
                            {
                                value = atributosImbutidos[5].Replace(",", ".");
                            }
                            break;
                        case "peso_cubico":
                            {
                                value = atributosImbutidos[5].Replace(",", ".");
                            }
                            break;
                        case "ncm":
                            {
                                value = atributosImbutidos[6].Replace(".", "");
                            }
                            break;
                        case "grupo_ncm":
                            {
                                value = atributosImbutidos[7];
                            }
                            break;
                        case "marcas":
                            {
                                value = atributosImbutidos[8];
                            }
                            break;
                        case "ipi":
                            {
                                value = atributosImbutidos[9];
                            }
                            break;
                        case "tributa_ipi":
                            {
                                value = atributosImbutidos[10];
                            }
                            break;
                        case "tributa_st":
                            {
                                // TODO: buscar uma maneira de procurar se tributa ST automaticamente sem a intervenção de um usuário cadastrar este dado
                                value = "0";
                            }
                            break;
                        default:
                            {
                                value = "";
                            }
                            break;
                    }

                    Attribute1 attribute = new()
                    {
                        attribute = atributo,
                        value = value,
                        order = "1",
                    };
                    attributes.Add(atributo, attribute);

                    recordset.MoveNext();
                    index++;
                }

                query = $"SELECT T1.\"U_Identificador\", T0.\"U_Value\" FROM \"@F1_ITEM_ATRIBUTOS\" T0 INNER JOIN \"@F1_ATRIBUTOS\" T1 ON T0.\"U_Atributo\" = T1.\"Code\" WHERE T0.\"U_ItemCode\" = '{code}'";
                recordset.DoQuery(query);
                while (!recordset.EoF)
                {
                    string atributo = Convert.ToString(recordset.Fields.Item(0).Value).Replace("/", "_").Replace("\\", "_");
                    string value = Convert.ToString(recordset.Fields.Item(1).Value);
                    if (attributes.TryGetValue(atributo, out Attribute1? attribute))
                    {
                        attribute.value = value;
                    }

                    recordset.MoveNext();
                }

                return attributes.Values.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao buscar os dados de atributos: {ex.Message}");
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);

            }
        }

        private static Price[] GetItemPriceList(string itemCode)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            try
            {
                double rate = ParserUteis.GetLastUSDRate();
                if (rate < 1)
                    rate = 1;
                string query = "SELECT T0.Price, T1.ListNum, T1.ListName FROM ITM1 T0 LEFT JOIN OPLN T1 ON T0.PriceList = T1.ListNum " +
                                $"WHERE T0.ItemCode = '{itemCode}' AND T1.ListNum in (Select distinct T2.ListNum FROM OCRD T2 WHERE T2.CardType = 'C')";
                recordset.DoQuery(query);
                Price[] price = new Price[recordset.RecordCount];
                int index = 0;
                while (!recordset.EoF)
                {
                    double value = (double)recordset.Fields.Item(0).Value;
                    value *= rate;
                    price[index] = new Price()
                    {
                        segmentation = recordset.Fields.Item(1).Value.ToString(),
                        value_for = value.ToString().Replace(",", "."),
                        value_of = value.ToString().Replace(",", "."),
                        spot_value = value.ToString().Replace(",", "."),
                        segmentation_name = recordset.Fields.Item(2).Value.ToString(),
                    };

                    index++;
                    recordset.MoveNext();
                }

                return price;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao buscar os dados de preços: {ex.Message}");
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }
        }

        private static Stock[] GetItemStock(string itemCode, string deadLine)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            try
            {
                string query = $"Select T0.\"WhsCode\", \"OnHand\", \"IsCommited\" from OITW T0 LEFT JOIN OWHS T1 ON T0.\"WhsCode\" = T1.\"WhsCode\" Where \"ItemCode\" = '{itemCode}' AND T1.\"U_ESTOQUE_F1\" = 'Y'";
                recordset.DoQuery(query);
                Stock[] stock = new Stock[recordset.RecordCount];
                int index = 0;
                while (!recordset.EoF)
                {
                    double onHand = recordset.Fields.Item(1).Value;
                    double comitted = recordset.Fields.Item(2).Value;
                    stock[index] = new Stock()
                    {
                        segmentation = recordset.Fields.Item(0).Value,
                        qty_reservation = comitted.ToString(),
                        qty_stock = onHand.ToString(),
                        ressuply_deadline = deadLine
                    };

                    recordset.MoveNext();
                    index++;
                }

                return stock;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao buscar os dados de estoque: {ex.Message}");
            }
            finally
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }
        }
    }
}