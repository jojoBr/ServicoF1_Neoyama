namespace ServicoF1.Models.F1.Regime_Tributario
{
    public sealed class RegimeResponse : Regime
    {
        public string updated_at { get; set; }
        public string created_at { get; set; }
        public int id { get; set; }

        public RegimeResponse()
        {
            updated_at = string.Empty;
            created_at = string.Empty;
        }
    }
}