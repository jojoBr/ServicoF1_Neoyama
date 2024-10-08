namespace ServicoF1.Models.F1.Produtos
{
    public sealed class Response
    {
        public string name { get; set; }
        public string code { get; set; }
        public string situation { get; set; }
        public int class_id { get; set; }
        public string updated_at { get; set; }
        public string created_at { get; set; }
        public int id { get; set; }

        public Response()
        {
            name = string.Empty;
            code = string.Empty;
            situation = string.Empty;
            updated_at = string.Empty;
            created_at = string.Empty;
        }
    }
}