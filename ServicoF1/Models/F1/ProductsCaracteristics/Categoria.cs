namespace ServicoF1.Models.F1.ProductsCaracteristics
{
    public sealed class Categoria
    {
        public string parent_category { get; set; }
        public string code { get; set; }
        public string external_code { get; set; }
        public int active { get; set; }
        public string name { get; set; }
        public int level { get; set; }
        public int order { get; set; }
        public int discount_max { get; set; }
        public int discount_fix { get; set; }

        public Categoria()
        {
            parent_category = string.Empty;
            code = string.Empty;
            external_code = string.Empty;
            name = string.Empty;
        }
    }
}