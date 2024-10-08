namespace ServicoF1.Models.F1.ProductsCaracteristics
{
    public sealed class CategoryResponse
    {
        public string code { get; set; }
        public int active { get; set; }
        public string name { get; set; }
        public int level { get; set; }
        public int order { get; set; }
        public int discount_max { get; set; }
        public int discount_fix { get; set; }
        public string updated_at { get; set; }
        public string created_at { get; set; }
        public int id { get; set; }

        public CategoryResponse()
        {
            code = string.Empty;
            name = string.Empty;
            updated_at = string.Empty;
            created_at = string.Empty;
        }
    }
}