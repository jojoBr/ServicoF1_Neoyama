namespace ServicoF1.Models.F1.ProductsCaracteristics
{
    public sealed class Categorias
    {
        public Categoria[] data { get; set; }

        public Categorias(int lenght)
        {
            data = new Categoria[lenght];
        }
    }
}