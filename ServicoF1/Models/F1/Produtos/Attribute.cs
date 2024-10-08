namespace ServicoF1.Models.F1.Produtos
{
    public sealed class Attribute
    {
        public string attribute { get; set; }
        public string group { get; set; }
        public int multiple { get; set; }

        public Attribute()
        {
            attribute = string.Empty;
            group = string.Empty;
        }
    }
}