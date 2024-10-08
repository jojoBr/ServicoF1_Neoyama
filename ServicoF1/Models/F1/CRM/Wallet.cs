namespace ServicoF1.Models.F1.CRM
{
    public sealed class Wallet
    {
        public string code { get; set; }
        public Seller[] sellers { get; set; }

        public Wallet(string code)
        {
            this.code = code;
            this.sellers = new Seller[1];
            sellers[0] = new Seller(code);
        }
    }

}