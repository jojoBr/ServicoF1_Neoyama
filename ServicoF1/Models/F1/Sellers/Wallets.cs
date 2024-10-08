using System.Text.Json.Serialization;

namespace ServicoF1.Models.F1.Sellers
{
    public class Wallets
    {
        [JsonPropertyName("data")]
        public Wallet[] Data { get; set; }

        public Wallets(int size)
        {
            Data = new Wallet[size];
        }
    }
}