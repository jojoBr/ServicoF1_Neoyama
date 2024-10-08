namespace ServicoF1.Models.F1.Matriz_Tributaria
{
    public sealed class MatrizResponse : Matriz
    {
        public string updated_at { get; set; }
        public string created_at { get; set; }
        public int id { get; set; }

        public MatrizResponse()
        {
            updated_at = string.Empty;
            created_at = string.Empty;
        }
    }
}