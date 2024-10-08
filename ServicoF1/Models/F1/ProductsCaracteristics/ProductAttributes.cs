using System.Text.Json.Serialization;

namespace ServicoF1.Models.F1.ProductsCaracteristics
{
    public sealed class ProductAttributes
    {
        public string? code { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
        public string? element_type { get; set; }
        public int id { get; set; }
        [JsonIgnore]
        public string? SapCode { get; set; }

        public ProductAttributes()
        {
            code = string.Empty;
            name = string.Empty;
            type = string.Empty;
        }
    }
}
