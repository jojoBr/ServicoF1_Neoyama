using System.Text.Json.Serialization;

namespace ServicoF1.Models.F1.CRMContacts
{
    public sealed class CRMContacts
    {
        public int active { get; set; }
        public string sector { get; set; }
        public string name { get; set; }
        public string mail { get; set; }
        public int is_telesales { get; set; }
        public float purchasing_limit { get; set; }
        public int visualize_only_owned_orders { get; set; }
        public int permission_place_orders { get; set; }
        public int permission_open_rma { get; set; }
        public int permission_get_orders { get; set; }
        public int permission_visualize_prices { get; set; }
        public int permission_allow_orders_ignoring_purchasing_limit { get; set; }
        [JsonIgnore]
        public int ContatctCode { get; set; }
        public int? id  { get; set; }
        [JsonPropertyName("crms")]
        public List<Crm> Crms { get; set; }

        public CRMContacts()
        {
            sector = string.Empty;
            name = string.Empty;
            mail = string.Empty;
            Crms = new List<Crm>();
        }
    }
}