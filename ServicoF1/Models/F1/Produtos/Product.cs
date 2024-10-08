using System.Text.Json.Serialization;

namespace ServicoF1.Models.F1.Produtos
{
    public sealed class Product
    {
        [JsonIgnore]
        public string F1_Id { get; set; }
        [JsonPropertyName("class")]
        public string _class { get; set; }
        public string type { get; set; }
        public string name { get; set; }
        public string code { get; set; }
        public string situation { get; set; }
        public string ipi { get; set; }
        public string ressuply_deadline { get; set; }
        public string multiple_inventory { get; set; }
        public string active { get; set; }
        public string export_date { get; set; }
        public Stock[] stock { get; set; }
        public New_Attributes[]? new_attributes { get; set; }
        public Attribute1[] attributes { get; set; }
        public Price[] prices { get; set; }
        public New_Class? new_class { get; set; }
        public New_Categories[]? new_categories { get; set; }
        public Category[] categories { get; set; }

        public Product() : this(0, 0, 0, 0)
        {
        }

        public Product(int stockLenght, int attributesLenght, int pricesLenght, int categoryLenght)
        {
            F1_Id = string.Empty;
            _class = string.Empty;
            type = string.Empty;
            name = string.Empty;
            code = string.Empty;
            situation = string.Empty;
            ipi = string.Empty;
            ressuply_deadline = string.Empty;
            active = string.Empty;
            export_date = string.Empty;
            stock = new Stock[stockLenght];
            attributes = new Attribute1[attributesLenght];
            prices = new Price[pricesLenght];
            categories = new Category[categoryLenght];
        }
    }
}