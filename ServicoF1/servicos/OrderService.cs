using ServicoF1.Converter;
using ServicoF1.Models.F1.Pedidos;
using ServicoF1.Models.WEB;
using SAPbobsCOM;
using ServicoF1.Uteis;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using ServicoF1.Models.F1.ProductsCaracteristics.Error;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Reflection.PortableExecutable;

namespace ServicoF1.servicos
{
    public class OrderService
    {
        private const string ORDER_URL = "https://apidatain-prod.f1ws.com.br/api/orders";
        private readonly string _token;
        private readonly bool _homolog;
        private readonly bool _test;
        private readonly string _client_test;
        private readonly ILogger _logger;
        private readonly API.ApiConnection _connection;
        private readonly Parser parser;

        /// <summary>
        /// Contrutor
        /// </summary>
        /// <param name="logger"> logger para os metodos</param>
        /// <param name="token"> token de acesso da F1 </param>
        /// <param name="homolog"> Deve usar a URL de homologação </param>
        /// <param name="test"> gerar o pedido com um código de teste para a F1 </param>
        /// <param name="client_test"> o cardCode do cliente para os pedidos de teste </param>
        public OrderService(ILogger logger, string token, bool homolog, bool test, string client_test)
        {
            _logger = logger;
            _token = token;
            _homolog = homolog;
            _connection = new API.ApiConnection(token, _logger);
            parser = new Parser(_logger);
            _test = test;
            _client_test = client_test;
        }

        // executa o workflow para o pedido de venda
        public void Run()
        {
            CadastraPedidos();
            CancelaPedidos();
        }

        private void CancelaPedidos()
        {
            _logger.LogInformation("Iniciando cancelamento de Pedidos");
            Order[] orders = GetOrders("CANCELED").Result;
            if (orders.Length < 1)
                return;

            _logger.LogInformation("{num} pedidos encontrados para o cancelamento", orders.Length);
            for (int index = 0; index < orders.Length; index++)
            {
                Order order = orders[index];
                try
                {
                    // para cada ordem tenta entrar nos documetos filhos
                    // e cancela eles, caso consiga continua cancelando até cancelar a ordem
                    CancelaPedido(order);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao Tentar cancelar o pedido {num}: {error}", order.code, ex.Message);
                    if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                    {
                        DIAPI.API.Reset = true;
                    }
                }
            }
        }

        private void CancelaPedido(Order order)
        {
            _logger.LogInformation("Tentando cancelar o pedido {id}", order.code);
            Header[] header = new Header[1] { new("Bearer", _token) };

            if (OrderExists(order.code!, true, out int doc))
            {
                if (DownPaymentExist(order.code!, out int adiantamentoKey))
                {
                    if (PaymentExist(order.code!, out int paymentKey))
                    {
                        Payments payment = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oIncomingPayments);
                        if (payment.GetByKey(paymentKey))
                        {
                            if (payment.Cancel() != 0)
                            {
                                _logger.LogError("Erro ao cancelar o pagamento para o pedido {order}| erro: {erro}", order.code, DIAPI.API.Company.GetLastErrorDescription());
                            }
                        }
                    }

                    Documents downPayment = (Documents)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oDownPayments);
                    if (downPayment.GetByKey(adiantamentoKey))
                    {
                        if (downPayment.Cancel() != 0)
                        {
                            _logger.LogError("Erro ao cancelar o adiantamento para o pedido {order}| erro: {erro}", order.code, DIAPI.API.Company.GetLastErrorDescription());
                        }
                    }
                }

                Documents document = (Documents)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oOrders);
                if (document.GetByKey(doc))
                {
                    if (document.Cancel() != 0)
                    {
                        _logger.LogError("Erro ao cancelar o pedido de venda para o pedido {order}| erro: {erro}", order.code, DIAPI.API.Company.GetLastErrorDescription());
                    }
                    else
                        UpdateOrderStatus(header, order.code!, "CANCELADO_ERP", "Cancelado ERP", _homolog, _connection, _logger);
                }
            }
            else
                UpdateOrderStatus(header, order.code!, "CANCELADO_ERP", "Cancelado ERP", _homolog, _connection, _logger);
        }

        /// <summary>
        /// Cadastra os pedidos de Vendas Da F1 para o SAP
        /// </summary>
        private void CadastraPedidos()
        {
            try
            {
                _logger.LogInformation("Iniciando Cadastro de Pedidos");
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                CRM_Service CRM = new(_logger, _token, _homolog);
                Order[] orders =  GetOrders("PAYMENTCONFIRMED").Result;
                if(orders.Length < 1)
                    return;

                //Cadastra os pedidos
                Span<Order> ordersSpan = orders.AsSpan();
                _logger.LogInformation("{num} Pedidos encontrados", ordersSpan.Length);
                for (int index = 0; index < ordersSpan.Length; index++)
                {
                    Order order = ordersSpan[index];
                    try
                    {
                        string test_sufix = _test ? "_test" : "";
                        if(InvoiceExists(order.code! + test_sufix))
                        {
                            _logger.LogInformation("Pedido {code}, já foi faturado", order.code);
                            continue;
                        }

                        if (!OrderExists(order.code! + test_sufix, false, out int orderKey))
                        {
                            #region Adiciona  O pedido adiantamento e pagamento
                            (string payementMethod, int paymentCondition) = ParserUteis.GetPaymentMethodAndCondition(order.payments![0]);

                            _logger.LogInformation("Cadastrando pedido {data}", order.code! + test_sufix);
                            Documents? documents = parser.Parse(order);
                            if (documents is null)
                                continue;

                            documents.PaymentMethod = payementMethod.Trim();
                            documents.PaymentGroupCode = paymentCondition;

                            _logger.LogInformation("dados de pagamento {metodo}, forma {form}", payementMethod, paymentCondition);
                            if (_test)
                            {
                                documents.NumAtCard += test_sufix;
                                documents.CardCode = _client_test;
                            }

                            if (documents.Add() == 0)
                            {

                                // vai salvar o numero do documento no campo user_code
                                _logger.LogInformation("Pedido {order} Cadastrado", order.code);
                                int key = int.Parse(DIAPI.API.Company.GetNewObjectKey());
                                documents.GetByKey(key);
                                //if (!_test)
                                //{
                                //    _logger.LogInformation("enviando id do pedido");
                                //    UpdateOrderField(header, order.code!, "user_code", documents.DocNum.ToString(), _homolog, _connection, _logger);
                                //}

                                if (order.payments![0].internal_type != "postbilledbankbill" && order.payments[0].internal_type != "BOLETO")
                                {
                                    Documents? downPayment = parser.Parse(order, documents);
                                    if (downPayment is null)
                                        continue;

                                    if (downPayment.Add() != 0)
                                    {
                                        string erro = DIAPI.API.Company.GetLastErrorDescription();
                                        _logger.LogError("Erro ao tentar adicionar o adiantamento do pedido de venda {id}: {error}", order.code, erro);
                                        continue;
                                    }
                                    _logger.LogInformation("adiantamento do pedido {order} Cadastrado", order.code);

                                    key = int.Parse(DIAPI.API.Company.GetNewObjectKey());
                                    downPayment.GetByKey(key);
                                    Payments? payments = parser.Parse(downPayment, order);
                                    if (payments is null)
                                        continue;

                                    if (payments.Add() != 0)
                                    {
                                        string erro = DIAPI.API.Company.GetLastErrorDescription();
                                        _logger.LogError("Erro ao tentar adicionar o pagamento do pedido de venda {id}: {error}", order.code, erro);
                                        continue;
                                    }
                                    _logger.LogInformation("pagamento do pedido {order} Cadastrado", order.code);
                                }

                                if (!_test)
                                    UpdateOrderStatus(header, order.code!, "integrado", "INTEGRADO", _homolog, _connection, _logger);
                            }
                            else
                            {
                                string error = DIAPI.API.Company.GetLastErrorDescription();
                                int errorCode = DIAPI.API.Company.GetLastErrorCode();
                                _logger.LogError("Erro ao tentar adicionar um pedido de venda {id}: {code}|{error}", order.code, errorCode, error);
                            }
                            #endregion
                        }
                        else if (!DownPaymentExist(order.code! + test_sufix, out int adiantamentoKey))
                        {
                            #region Adiciona  adiantamento e pagamento caso tenha ocorrido um erro
                            if (order.payments![0].internal_type != "postbilledbankbill" && order.payments[0].internal_type != "BOLETO")
                            {
                                _logger.LogInformation("Tentando cadastrar o adiantamento com erro do pedido: {key}", orderKey);
                                Documents? documents = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oOrders);
                                documents.GetByKey(orderKey);
                                Documents? downPayment = parser.Parse(order, documents);
                                if (downPayment is null)
                                    continue;

                                if (downPayment.Add() != 0)
                                {
                                    string erro = DIAPI.API.Company.GetLastErrorDescription();
                                    _logger.LogError("Erro ao tentar adicionar o adiantamento do pedido de venda {id}: {error}", order.code, erro);
                                    continue;
                                }
                                int key = int.Parse(DIAPI.API.Company.GetNewObjectKey());
                                downPayment.GetByKey(key);
                                Payments? payments = parser.Parse(downPayment, order);
                                if (payments is null)
                                    continue;

                                if (payments.Add() != 0)
                                {
                                    string erro = DIAPI.API.Company.GetLastErrorDescription();
                                    _logger.LogError("Erro ao tentar adicionar o pagamento do pedido de venda {id}: {error}", order.code, erro);
                                    continue;
                                }
                            }

                            if (!_test)
                                UpdateOrderStatus(header, order.code!, "integrado", "INTEGRADO", _homolog, _connection, _logger);
                            #endregion
                        }
                        else if (!PaymentExist(order.code! + test_sufix, out int key))
                        {
                            #region Adiciona  pagamento caso tenha ocorrido um erro
                            if (order.payments![0].internal_type != "postbilledbankbill" && order.payments[0].internal_type != "BOLETO")
                            {
                                _logger.LogInformation("Tentando cadastrar o pagamento com erro do adiantameno: {key}", adiantamentoKey);
                                Documents downPayment = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oDownPayments);
                                if (downPayment.GetByKey(adiantamentoKey))
                                {
                                    Payments? payments = parser.Parse(downPayment, order);
                                    if (payments is null)
                                        continue;

                                    if (payments.Add() != 0)
                                    {
                                        string erro = DIAPI.API.Company.GetLastErrorDescription();
                                        _logger.LogError("Erro ao tentar adicionar um pagamento do pedido de venda {id}: {error}", order.code, erro);
                                    }

                                    if (!_test)
                                        UpdateOrderStatus(header, order.code!, "integrado", "INTEGRADO", _homolog, _connection, _logger);
                                }
                                else
                                    _logger.LogError("Não foi possivel acessar o adiantamento com chave {key}", adiantamentoKey);
                            }
                            #endregion
                        }
                        else
                        {
                            if (!_test)
                                UpdateOrderStatus(header, order.code!, "integrado", "INTEGRADO", _homolog, _connection, _logger);
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError("Erro ao Tentar Cadastrar o pedido {id}: {error}", order.code, ex.Message);
                        if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                        {
                            DIAPI.API.Reset = true;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao Tentar Cadastrar os pedidos: {error}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
            }
        }

        /// <summary>
        /// Verifica se um contas a receber foi gerado para o pedido de venda
        /// </summary>
        /// <param name="numAtCard"> referencia do pedido da F1</param>
        /// <param name="key"> DocEntry do contas a receber ou -1 se não tiver um contas criado</param>
        /// <returns> true caso exista</returns>
        private static bool PaymentExist(string numAtCard, out int key)
        {
            key = -1;
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = Queries.Find_Pagamento.Replace("{card}", numAtCard);
            recordset.DoQuery(query);
            if(recordset.RecordCount > 0)
            {
                key = recordset.Fields.Item(0).Value;
                return true;
            }

            //query = Queries.Find_Adiantamento.Replace("{card}", numAtCard);
            //recordset.DoQuery(query);
            //key = recordset.Fields.Item(0).Value;
            return false;
        }

        /// <summary>
        /// Verifica se um adiantamento foi gerado para o pedido de venda
        /// </summary>
        /// <param name="numAtCard"> referencia do pedido da F1</param>
        /// <param name="key"> DocEntry do adiantamento  ou -1 se não tiver um contas criado</param>
        /// <returns> true caso exista</returns>
        private static bool DownPaymentExist(string numAtCard, out int key)
        {
            key = -1;
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = Queries.Find_Adiantamento.Replace("{card}", numAtCard);
            recordset.DoQuery(query);
            if (recordset.RecordCount > 0)
            {
                key = recordset.Fields.Item(0).Value;
                return true;
            }

            //query = Queries.Find_Order.Replace("{card}", numAtCard);
            //recordset.DoQuery(query);
            //key = recordset.Fields.Item(0).Value;
            return false;
        }

        /// <summary>
        /// Atualiza o status do pedido da F1
        /// </summary>
        /// <param name="header"> headers da requisição </param>
        /// <param name="orderCode"> código do pedido a ser atualizado</param>
        /// <param name="field"> campo ao atualizar </param>
        /// <param name="value"> valor do campo </param>
        /// <param name="homolog"> é para a url de homologação </param>
        /// <param name="connection"> objeto de HTTPS </param>
        /// <param name="logger"> Logger </param>
        public static void UpdateOrderStatus(Header[] header, string orderCode, string status, string name, bool homolog, API.ApiConnection connection, ILogger logger)
        {
            try
            {
                logger.LogInformation("atualizando status do pedido {id}, status {id}/{name}", orderCode, status, name);

                string url = $"{ORDER_URL}/{orderCode}";
                if (homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");
                _ = connection.PUTString(url, header, $"{{\"status_id\":\"{status}\", \"status\":\"{name}\"}}", false).Result;
            }
            catch (Exception ex)
            {
                logger.LogError("Erro ao tentar atualizar o status do pedido para Recebido: {erro}", ex.Message);
            }
        }

        /// <summary>
        /// Verifica se o pedido de venda já foi Cadastrado
        /// </summary>
        /// <param name="code"> Código do pedido da F1</param>
        /// <returns> true caso exista </returns>
        private static bool OrderExists(string code, bool includeClosed, out int key)
        {
            key = -1;
            string closed = includeClosed ? "AND DocStatus = 'O'" : "";
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = $"SELECT DocEntry FROM ORDR WHERE NumAtCard = '{code.Trim()}' AND CANCELED = 'N' {closed}";
            recordset.DoQuery(query);
            if (recordset.RecordCount > 0)
            {
                key = recordset.Fields.Item(0).Value;
                return true;
            }

            return false;
        }

        private static bool InvoiceExists(string code)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = $"SELECT DocEntry FROM OINV WHERE NumAtCard = '{code.Trim()}' AND CANCELED = 'N'";
            recordset.DoQuery(query);
            bool value = recordset.RecordCount > 0;
            Marshal.ReleaseComObject(recordset);
            return value;
        }

        /// <summary>
        /// Busca os pedidos da F1 com Status de PAYMENTCONFIRMED
        /// </summary>
        /// <returns> Array de pedidos </returns>
        public async Task<Order[]> GetOrders(string status)
        {
            try
            {
                string url = ORDER_URL + $"?statusId={status}";
                Header[] header = new Header[1] { new Header("Bearer", _token) };
                if (_homolog)
                    url = url.Replace("apidatain-prod", "apidatain-homolog");
                Orders? orders = await _connection.GET<Orders>(url, header);
                if (orders != null)
                {
                    while (!string.IsNullOrEmpty(orders.next_page_url))
                    {
                        Orders? temp = await _connection.GET<Orders>(orders.next_page_url, header);
                        if (temp != null)
                        {
                            orders.data!.AddRange(temp.data!);
                            orders.next_page_url = temp.next_page_url;
                        }
                        else
                            orders.next_page_url = string.Empty;
                    }

                    return orders.data!.ToArray();
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao buscar os pedidos: {error}", ex.Message);
            }

            return Array.Empty<Order>();
        }
    }
}