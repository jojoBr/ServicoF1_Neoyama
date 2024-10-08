namespace ServicoF1.Models.F1.ProductsCaracteristics
{
    public sealed class ClassResponse
    {
        public string code { get; set; }
        public string name { get; set; }
        public string updated_at { get; set; }
        public string created_at { get; set; }
        public int id { get; set; }

        public ClassResponse()
        {
            code = string.Empty;
            name = string.Empty;
            updated_at = string.Empty;
            created_at = string.Empty;
        }
    }
}