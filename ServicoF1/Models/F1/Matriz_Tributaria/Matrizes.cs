namespace ServicoF1.Models.F1.Matriz_Tributaria
{
    public sealed class Matrizes
    {
        public Matriz[] data { get; set; }

        public Matrizes(int lenght)
        {
            data = new Matriz[lenght];
        }
    }
}