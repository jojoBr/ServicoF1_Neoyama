namespace ServicoF1.Models.F1.Produtos
{
    public sealed class New_Attributes
    {
        public string code { get; set; }
        public string name { get; set; }
        public string type { get; set; }

        public New_Attributes()
        {
            code = string.Empty;
            name = string.Empty;
            type = string.Empty;
        }
    }
}