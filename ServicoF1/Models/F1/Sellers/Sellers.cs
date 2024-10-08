using System.Text.Json.Serialization;

namespace ServicoF1.Models.F1.Sellers
{
    internal class Sellers
    {
        [JsonPropertyName("data")]
        public Seller[] Data { get; set; }

        public Sellers(int size)
        {
            Data = new Seller[size];
        }
    }
}