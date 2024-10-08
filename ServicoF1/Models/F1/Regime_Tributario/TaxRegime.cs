namespace ServicoF1.Models.F1.Regime_Tributario
{
    public sealed class TaxRegime
    {
        public List<Regime> data { get; set; }

        public TaxRegime(int lenght)
        {
            data = new List<Regime>(lenght);
        }
    }
}