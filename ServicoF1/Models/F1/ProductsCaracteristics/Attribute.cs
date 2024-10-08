namespace ServicoF1.Models.F1.ProductsCaracteristics
{
    public sealed class Attribute
    {
        public string attribute_key { get; set; }
        public string group { get; set; }
        public int multiple { get; set; }
        public int variation { get; set; }

        public Attribute(string key, string group)
        {
            attribute_key = key;
            this.group = group;
            this.multiple = 0;
            variation = 0;
        }
    }
}