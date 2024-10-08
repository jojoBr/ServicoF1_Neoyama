using SAPbobsCOM;
using ServicoF1.API;
using ServicoF1.Models.F1.ExternalOrders;
using ServicoF1.Models.WEB;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ServicoF1.servicos
{
    public class ExternalOrdersService
    {
        private const string EXTERNAL_ORDERS_URL = "https://apidatain-prod.f1ws.com.br/api/external_orders";
        private readonly string _token;
        private readonly bool _homolog;
        private readonly ILogger _logger;
        private readonly ApiConnection _connection;

        public ExternalOrdersService(string token, bool homolog, ILogger logger)
        {
            _token = token;
            _homolog = homolog;
            _logger = logger;
            _connection = new API.ApiConnection(token, _logger);
        }

        public void Run(bool DataSendIntoLogger = false)
        {
            ExternalOrders externalOrders = GetExternalOrders();
            SendOrders(externalOrders, DataSendIntoLogger);
        }

        private async void SendOrders(ExternalOrders externalOrders, bool DataSendIntoLogger)
        {
            if (externalOrders.data.Count < 1)
                return;

            Header[] header = new Header[1] { new Header("Bearer", _token) };
            string url = EXTERNAL_ORDERS_URL;
            if (_homolog)
                url = url.Replace("apidatain-prod", "apidatain-homolog");

            try
            {
                if (DataSendIntoLogger)
                    _logger.LogInformation("Pedidos externos Data: {dados}", JsonSerializer.Serialize(externalOrders));
                // a api não retorno nada, o serviço simplesmente vai jogar um erro caso tenha ocorrido um problema ao enviar os pedidos
                _ = await _connection.POST<ExternalOrders, ExternalOrder[]>(url, header, externalOrders);
                Documents invoices = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oInvoices);
                Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);

                foreach (var invoice in externalOrders.data)
                {
                    string query = $"UPDATE OINV set U_Id_Externo_F1 = {invoice.code} WHERE DocEntry = {invoice.code}";
                    recordset.DoQuery(query);
                }

                Marshal.ReleaseComObject(recordset);
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar enviar os pedidos Externos erro {erro}", ex.Message);
            }
        }

        private ExternalOrders GetExternalOrders()
        {
            try
            {
                Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string companyId = _homolog ? "101" : "1";
                string query = $@"SELECT
                            T0.DocEntry, --0
                            CASE
                            WHEN LEN(T5.TaxId0) > 0 THEN TaxId0  
                            WHEN LEN(T5.TaxId4) > 0 THEN TaxId4
                            END AS 'Document', --1
                            t0.DocTotal, --2
                            t6.LineTotal, --3
                            T1.ItemCode, --4
                            T7.ItemName, --5
                            T1.Quantity, --6
                            T1.Price, --7
                            T1.Price * T1.Quantity AS 'Total Price', --8
                            T0.Serial, --9
                            T0.SeriesStr, --10
                            T4.KeyNfe, --11
                            T0.DocDate --12
                            FROM OINV T0 
                            INNER JOIN INV1 T1 ON T0.DocEntry = T1.DocEntry
                            INNER JOIN OITM T7 ON T1.ItemCode = T7.ItemCode
                            INNER JOIN OCRD T2 ON T0.CardCode = T2.CardCode
                            INNER JOIN CRD7 T5 ON T2.CardCode = T5.CardCode
                            LEFT JOIN DBInvOne.dbo.Process T4 ON T0.DocEntry = T4.DocEntry and t4.DocType = 13 and T4.CompanyId = {companyId} and (T4.StatusId = '4' or T0.U_MMQStatusNFe = '5')
                            LEFT JOIN INV3 T6 ON T0.DocEntry = T6.DocEntry and T6.ExpnsCode = 1
                            WHERE T0.U_proposta_crm not Like 'f1%' 
                            and T0.model = '39' and T0.CANCELED <> 'Y' AND T0.CANCELED <> 'C'
                            and ISNULL(T2.U_CadastraF1, 'N') = 'Y' and T2.U_F1_Id is not null
                            and ISNULL(T0.U_Id_Externo_F1, '') = ''";
                recordset.DoQuery(query);
                _logger.LogInformation("Foram Encontrados {num} Pedidos externos", recordset.RecordCount);
                ExternalOrders externalOrders = new ExternalOrders();
                while (!recordset.EoF)
                {
                    string code = Convert.ToString(recordset.Fields.Item(0).Value);
                    string document = Convert.ToString(recordset.Fields.Item(1).Value);
                    if (string.IsNullOrEmpty(document))
                    {
                        recordset.MoveNext();
                        continue;
                    }

                    ExternalOrder? externalOrder = externalOrders.data.FirstOrDefault(x => x.code == code);
                    if (externalOrder != null)
                    {
                        externalOrder.items.Add(new Item()
                        {
                            product_code = Convert.ToString(recordset.Fields.Item(4).Value),
                            product_name = Convert.ToString(recordset.Fields.Item(5).Value),
                            quantity = Convert.ToInt32(recordset.Fields.Item(6).Value),
                            unitary_value = (float)Convert.ToDouble(recordset.Fields.Item(7).Value),
                            total_value = (float)Convert.ToDouble(recordset.Fields.Item(8).Value),
                        });
                    }
                    else
                    {
                        externalOrder = new()
                        {
                            code = code,
                            client_cpfj = document,
                            total_value = (float)Convert.ToDouble(recordset.Fields.Item(2).Value),
                            freight_value = (float)Convert.ToDouble(recordset.Fields.Item(3).Value),
                            status = "FINALIZADO"
                        };

                        //item
                        externalOrder.items.Add(new Item()
                        {
                            product_code = Convert.ToString(recordset.Fields.Item(4).Value),
                            product_name = Convert.ToString(recordset.Fields.Item(5).Value),
                            quantity = Convert.ToInt32(recordset.Fields.Item(6).Value),
                            unitary_value = (float)Convert.ToDouble(recordset.Fields.Item(7).Value),
                            total_value = (float)Convert.ToDouble(recordset.Fields.Item(8).Value),
                        });

                        string val = Convert.ToString(recordset.Fields.Item(12).Value);
                        //_logger.LogInformation("data, {data}", val[..10]);
                        DateTime date = string.IsNullOrEmpty(val) ? DateTime.Now : DateTime.ParseExact(val[..10], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        // notas fiscal
                        externalOrder.invoices.Add(new Invoice()
                        {
                            number = Convert.ToString(recordset.Fields.Item(9).Value),
                            serie = Convert.ToString(recordset.Fields.Item(10).Value),
                            key = Convert.ToString(recordset.Fields.Item(11).Value),
                            date = date.ToString("yyyy-MM-dd"),
                        });

                        externalOrders.data.Add(externalOrder);
                    }
                    recordset.MoveNext();
                }

                return externalOrders;
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao tentar Buscar as ordens externas: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return new ExternalOrders();
            }
        }
    }
}