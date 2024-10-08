namespace ServicoF1.Models.F1.ProductsCaracteristics
{
    public sealed class Class
    {
        public string parent_class { get; set; }
        public string code { get; set; }
        public string name { get; set; }
        public List<Attribute> attributes { get; set; }

        public Class()
        {
            parent_class = string.Empty;
            code = string.Empty;
            name = string.Empty;
            attributes = new List<Attribute>();
        }
    }
}