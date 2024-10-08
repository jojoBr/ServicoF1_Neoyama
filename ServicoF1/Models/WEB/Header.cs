namespace ServicoF1.Models.WEB
{
    public sealed class Header
    {
        public string Type { get;}
        public string Value { get;}

        public Header(string type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}