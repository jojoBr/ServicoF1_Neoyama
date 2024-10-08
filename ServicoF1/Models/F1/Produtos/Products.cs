namespace ServicoF1.Models.F1.Produtos
{
    public sealed class Products
    {
        public Product[] data { get; set; }

        public Products(int lenght)
        {
            data = new Product[lenght];
        }
        public Products(Product[] products)
        {
            data = products;
        }
    }
}