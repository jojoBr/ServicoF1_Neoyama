using System.Text.Json.Serialization;

namespace ServicoF1.Models.F1.NotaFiscal
{
    public class Invoice
    {
        [JsonIgnore]
        public string id { get; init; }
        public string base64_file { get; set; }
        public string code { get; set; }
        public string type { get; set; }
        public string distribution_center_code { get; set; }
        public string number { get; set; }
        public string serie { get; set; }
        public string key { get; set; }
        public DateTime invoice_date { get; set; }
        public string tracking_code { get; set; }
        public string tracking_url { get; set; }
        public string transport_code { get; set; }
        public string transport_name { get; set; }
        public string delivery_date { get; set; }
        public string billing_type { get; set; }
        public Bankbill[] bankbills { get; set; }
        public List<Item> items { get; set; }

        public Invoice(string id)
        {
            this.id = id;
        }
    }
}