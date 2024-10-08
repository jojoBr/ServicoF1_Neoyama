using System.Text.Json.Serialization;

namespace ServicoF1.Models.F1.Condicao_de_Pagamento
{
    public class Installment
    {
        public int? id { get; set; }
        public int? payment_condition_id { get; set; }
        public string code { get; set; }
        public int installment_number { get; set; }
        public string display { get; set; }
        public string type { get; set; }
        public string percent { get; set; }
        public string minimum_value { get; set; }
        public int post_payment { get; set; }
        public int average_period_payment { get; set; }
        public string channel { get; set; }
        public string initial_status { get; set; }
        [JsonIgnore]
        public string U_type { get; set; }
    }

}
