using SAPbobsCOM;
using ServicoF1.Models.F1.Pedidos;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ServicoF1.Uteis
{
    internal class ParserUteis
    {
        /// <summary>
        /// busca o id do endereço com base no zipcode do endereço enviado como argumento assim como o tipo do endreço 
        /// e o parceiro, tipo do endreço B(Billing) ou S(Shipping)
        /// </summary>
        /// <param name="cardCode"> code do parceiro </param>
        /// <param name="zipCode"> CEP do endereço </param>
        /// <param name="type"> tipo do endreço B(Billing) ou S(Shipping) </param>
        /// <returns> o code do endereço ou vazio caso não encontre </returns>
        public static string GetAddrIndex(string cardCode, string zipCode, string type)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = $"SELECT Address FROM CRD1 WHERE CardCode = '{cardCode}' AND ZipCode = '{zipCode}' AND AdresType = '{type}'";
            recordset.DoQuery(query);
            string response = "";
            if (recordset.RecordCount > 0)
                response = (string)recordset.Fields.Item(0).Value;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Marshal.FinalReleaseComObject(recordset);

            return response;
        }

        /// na F1 a utilização do item não é selecionada pelo usuário mas sim pelo CRM
        /// e o CRM faz isso utilizando a Tabela AKI_Utilizacao, utilizando a utilizacao da venda
        /// e o tipo do item
        /// <summary>
        /// Busca a utilização do item com base no itemCode e utilização da venda
        /// </summary>
        /// <param name="sellUsage"> utilização na venda </param>
        /// <param name="itemCode"> código do item </param>
        /// <returns> a utilização do item, e o deposito caso aja algum </returns>
        public static (int?, int?) GetItemUsage(string sellUsage, string itemCode)
        {
            // Tabela será utilizada sera um clone da tabela Aki_Utilizacao
            // o id desta tabela sera o tipo de venda dentro do desta Tabela
            // e ela irá possuir duas utilizações com base no tipo do item 0 para revenda e 4 para venda
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            int? usage = null;
            int? deposito = null;
            string queryItem = $"Select MatType FROM OITM WHERE ItemCode = '{itemCode}'";
            recordset.DoQuery(queryItem);
            if (recordset.RecordCount > 0) 
            {
                string type = Convert.ToString(recordset.Fields.Item(0).Value);
                string queryUsage = $@"SELECT U_Cd_Venda_SAP, U_Cd_Revenda_SAP, U_Cd_Deposito FROM ""@F1_UTILIZACAO"" WHERE Code = '{sellUsage}' AND U_status = 'A'";
                recordset.DoQuery(queryUsage);
                if (recordset.RecordCount > 0)
                {
                    deposito = recordset.Fields.Item(2).Value;
                    if (type == "4")
                        usage = recordset.Fields.Item(0).Value;
                    else if(type == "0")
                        usage = recordset.Fields.Item(1).Value;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Marshal.FinalReleaseComObject(recordset);
            
            return (usage, deposito);
        }

        public static double GetLastUSDRate()
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string rateQuery = $"SELECT Rate FROM ORTT WHERE Currency = 'USD' AND RateDate = '{DateTime.Now:yyyyMMdd}'";
            recordset.DoQuery(rateQuery);
            if (recordset.RecordCount < 1)
            {
                // busca a maior data ou seja a ultima data com um valor de USD
                rateQuery = "SELECT Rate FROM ORTT WHERE Currency = 'USD' AND RateDate = (SELECT MAX(RateDate) FROM ORTT WHERE Currency ='USD' AND ISNULL(Rate, 0) > 0  and RateDate < GetDate())";
                recordset.DoQuery(rateQuery);
            }
            double rate = Convert.ToDouble(recordset.Fields.Item(0).Value, new CultureInfo("pt-BR"));
            return rate;
        }

        internal static string GetClientAreaNegocio(string? client_code)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = $"SELECT U_Centro_Custo FROM \"@F1_UNIDADE_NEGOCIO\" WHERE ISNULL(U_Inativo, 'N') = 'N' AND Name = (SELECT U_Area_negocio FROM OCRD WHERE CardCode = '{client_code}')";
            recordset.DoQuery(query);
            if (recordset.RecordCount > 0)
                return recordset.Fields.Item(0).Value;

            return string.Empty;
        }

        /// <summary>
        /// busca os dados da conta para o alias do F1
        /// </summary>
        /// <param name="alias"> alias do pagamento </param>
        /// <param name="isForeign"> o cliente é estrangeiro </param>
        /// <returns>Retorna o tipo de pagamento e a conta a ser usada nesta ordem</returns>
        public static (string, string) GetPaymentAccount(string alias, bool isForeign)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string foreign = isForeign ? "Y" : "N";
            string query = $"SELECT U_TIPO, U_conta FROM \"@F1_PAYMENT_CONF\" WHERE U_Estrangeiro = '{foreign}' AND U_Alias = '{alias}'";
            recordset.DoQuery(query);
            if (recordset.RecordCount < 0)
                return (string.Empty, string.Empty);
            return (Convert.ToString(recordset.Fields.Item(0).Value), Convert.ToString(recordset.Fields.Item(1).Value));
        }

        /// <summary>
        /// get the paymentform and condition
        /// </summary>
        /// <param name="payment"></param>
        /// <returns> payment form, and conditions in this order</returns>
        internal static (string, int) GetPaymentMethodAndCondition(Payment? payment)
        {
            if(payment is null)
                return (string.Empty, -1);

            // 317 é a condição de pagamento A VISTA ECOMMERCE o valor 1 é o padrão da F1
            return (GetPaymentForm(payment.internal_type!), payment.external_code == "1" ? GetPaymentConditionCode("A VISTA ECOMMERCE") : int.Parse(payment.external_code!));
            //if (payment.internal_type == "card" || payment.internal_type == "pix" || payment.internal_type == "transfer")
            //{
            //    return ("Bol.Santader1", GetPaymentConditionCode("A VISTA ECOMMERCE"));
            //}
            //else
            //{
            //    return ("Bol.Itau Garant", GetPaymentConditionCode("A VISTA ECOMMERCE"));
            //}
        }

        private static string GetPaymentForm(string internalId)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = $"SELECT U_Forma_Pagamento FROM \"@F1_PAYMENT_CONF\" WHERE U_Alias = '{internalId}'";
            recordset.DoQuery(query);
            if (recordset.RecordCount > 0)
                return Convert.ToString(recordset.Fields.Item(0).Value);
            return string.Empty;
        }

        private static int GetPaymentConditionCode(string conditionName)
        {
            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = $"SELECT GroupNum FROM OCTG WHERE PymntGroup = '{conditionName}'";
            recordset.DoQuery(query);
            if(recordset.RecordCount > 0)
                return Convert.ToInt32(recordset.Fields.Item(0).Value);
            return -1;
        }

        internal static string GetCarrier(string? freight_id_transport)
        {
            if (string.IsNullOrEmpty(freight_id_transport))
                return "";

            Recordset recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = $"SELECT CardCode FROM OCRD WHERE U_id_intelipost = {freight_id_transport}";
            recordset.DoQuery(query);
            if (recordset.RecordCount > 0)
                return Convert.ToString(recordset.Fields.Item(0).Value);
            return "";
        }
    }
}