using System.Text.Json.Serialization;

namespace ServicoF1.Models.F1.Regime_Tributario
{
    public class Regime
    {
        public string code { get; set; }
        public string name { get; set; }
        public int modified { get; set; }
        [JsonIgnore]
        public int F1_ID { get; set; }
        public string export_date { get; set; }

        public Regime()
        {
            code = string.Empty;
            name = string.Empty;
            export_date = string.Empty;
        }
    }
}