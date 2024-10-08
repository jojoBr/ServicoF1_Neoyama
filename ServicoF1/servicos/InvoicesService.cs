using SAPbobsCOM;
using ServicoF1.Models.F1.NotaFiscal;
using ServicoF1.Models.WEB;
using System.Runtime.InteropServices;

namespace ServicoF1.servicos
{
    public class InvoicesService
    {
        private const string INVOICES_URL = "https://apidatain-prod.f1ws.com.br/api/orders/{0}/invoices";
        private readonly string _token;
        private readonly int _compId;
        private readonly bool _homolog;
        private readonly ILogger _logger;
        private readonly API.ApiConnection _connection;
        private Documents Nota;

        public InvoicesService(ILogger logger, string token, bool homolog, int compId)
        {
            _logger = logger;
            _token = token;
            _homolog = homolog;
            _connection = new API.ApiConnection(token, _logger);
            Nota = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oInvoices);
            _compId = compId;
        }

        public void Run()
        {
            Invoice[] invoices = GetInvoices();
            UpdateInvoices(invoices);
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Marshal.FinalReleaseComObject(Nota);
        }

        private async void UpdateInvoices(Invoice[] invoices)
        {
            if (invoices.Length < 1)
                return;

            Header[] header = new Header[1] { new Header("Bearer", _token) };
            string url = INVOICES_URL;
            if (_homolog)
                url = url.Replace("apidatain-prod", "apidatain-homolog");

            foreach (Invoice invoice in invoices)
            {
                try
                {
                    string uri = string.Format(url, invoice.id);
                    Invoices data = new(new Invoice[1] { invoice });
                    await _connection.POST<Invoices, Invoice[]>(uri, header, data);
                    AtualizaStatusDaFila(invoice.code);
                    OrderService.UpdateOrderStatus(header, invoice.id!, "INVOICECREATED", "Nota Gerada", _homolog, _connection, _logger);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao tentar atualizar a nota do pedido: {nota}, erro: {erro}", ex.Message, invoice.id);
                }
            }
        }

        private void AtualizaStatusDaFila(string code)
        {
            if (Nota.GetByKey(int.Parse(code)))
            {
                Nota.UserFields.Fields.Item("U_NFEnviada_F1").Value = "Y";
                Nota.Update();
            }
        }

        public Invoice[] GetInvoices()
        {
            try
            {
                int companyId = _homolog? 101 : _compId;
                Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
                string query = $@"SELECT 
                            'NFS' AS 'Tipo' --0
                             ,T0.DocEntry --1
                             ,T0.Serial --2
                             ,T0.DocDate --3
                             ,T0.NumAtCard --4
                             ,(select sum(LineTotal + VatSum + DistribSum + TaxDistSum + StckDstSum) from
                              INV1 where INV1.docentry = T0.docentry) as total --5
                             , T1.PymntGroup --6
                             , T2.LINETOTAL as frete --7
                             , T0.U_MMQChaveNFe --8
                             , t0.U_proposta_crm --9
                             , T4.KeyNfe --10
                             ,T0.SeriesStr --11
                                FROM OINV T0
                                LEFT JOIN inv3 T2 ON T2.DOCENTRY = T0.DOCENTRY and T2.expnscode = 3
                                INNER JOIN OCTG T1 ON T0.GROUPNUM = T1.GROUPNUM
                                LEFT JOIN DBInvOne.dbo.Process T4 ON T0.DocNum = T4.DocEntry and t4.DocType = 13
                                where ISNULL(T0.U_NFEnviada_F1, 'N') <> 'Y' and T0.U_proposta_crm Like 'f1%' 
                                and T0.model = '39' and CANCELED <> 'Y' AND CANCELED <> 'C'
                                and (T4.StatusId = '4'  or  T0.U_MMQStatusNFe = '5')
                                and T4.CompanyId = {companyId}";
                recordset.DoQuery(query);
                Dictionary<string, Invoice> invoices = new Dictionary<string, Invoice>();
                while (!recordset.EoF)
                {
                    string id = Convert.ToString(recordset.Fields.Item(1).Value);
                    string keyReference = Convert.ToString(recordset.Fields.Item(10).Value);

                    Invoice invoice = new(id)
                    {
                        code = id,
                        id = Convert.ToString(recordset.Fields.Item(9).Value),
                        type = "SALE",
                        key = keyReference,
                        number = Convert.ToString(recordset.Fields.Item(2).Value),
                        serie = Convert.ToString(recordset.Fields.Item(11).Value),
                        invoice_date = (DateTime)recordset.Fields.Item(3).Value,
                        distribution_center_code = "20" // padrão no momento só tem um
                    };
                    invoices.Add(id, invoice);
                    recordset.MoveNext();
                }

                return invoices.Values.ToArray();
            }
            catch(Exception ex)
            {
                _logger.LogError("Erro ao buscar as notas: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return Array.Empty<Invoice>();
            }
        }

        private string GetFileBase64(string? value)
        {
            if(string.IsNullOrEmpty(value))
                return string.Empty;
            byte[] bytes = File.ReadAllBytes(value);
            string base64 = Convert.ToBase64String(bytes);
            return base64;
        }
    }
}