namespace ServicoF1.Models.F1.Produtos
{
    public sealed class New_Categories
    {
        public string parent_category { get; set; }
        public string code { get; set; }
        public string active { get; set; }
        public string name { get; set; }
        public string level { get; set; }
        public string order { get; set; }
        public string discount_max { get; set; }
        public string discount_fix { get; set; }

        public New_Categories()
        {
            parent_category = string.Empty;
            code = string.Empty;
            active = string.Empty;
            name = string.Empty;
            level = string.Empty;
            order = string.Empty;
            discount_max = string.Empty;
            discount_fix = string.Empty;
        }
    }
}