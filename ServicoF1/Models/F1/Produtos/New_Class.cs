namespace ServicoF1.Models.F1.Produtos
{
    public sealed class New_Class
    {
        public string parent_class { get; set; }
        public string code { get; set; }
        public string name { get; set; }
        public Attribute[] attributes { get; set; }

        public New_Class() : this(0)
        {
        }

        public New_Class(int lenght)
        {
            parent_class = string.Empty;
            code = string.Empty;
            name = string.Empty;
            attributes = new Attribute[lenght];
        }
    }
}