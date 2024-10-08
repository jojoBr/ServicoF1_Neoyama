namespace ServicoF1.Models.F1.ProductsCaracteristics
{
    public sealed class ProductsAttributes
    {
        public ProductAttributes[] data { get; set; }

        public ProductsAttributes(int lenght)
        {
            data = new ProductAttributes[lenght];
        }
    }
}