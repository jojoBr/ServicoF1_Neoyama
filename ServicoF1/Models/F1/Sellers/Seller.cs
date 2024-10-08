namespace ServicoF1.Models.F1.Sellers
{
    public class Seller
    {
        public string code { get; set; }
        public int active { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public int? id { get; set; }
        public Wallet[] wallets { get; set; }

        public Seller()
        {
            code = string.Empty;
            active = 0;
            name = string.Empty;
            email = string.Empty;
            wallets = new Wallet[1];
        }

        public Seller(int walletSize)
        {
            code = string.Empty;
            active = 0;
            name = string.Empty;
            email = string.Empty;
            wallets = new Wallet[walletSize];
        }
    }
}