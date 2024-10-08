namespace ServicoF1.Models.F1.Sellers
{
    public class Wallet
    {
        public string code { get; set; }
        public int? active { get; set; }
        public string? name { get; set; }

        public Wallet()
        {
            this.code = string.Empty;
        }
    }
}