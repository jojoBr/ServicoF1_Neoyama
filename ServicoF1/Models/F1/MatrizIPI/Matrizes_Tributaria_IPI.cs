namespace ServicoF1.Models.F1.MatrizIPI
{
    public class Matrizes_Tributaria_IPI
    {
        public List<Matriz_Tributaria_IPI> data { get; set; }
        
        /// <summary>
        /// class constructor
        /// </summary>
        /// <param name="size"> number of values </param>
        public Matrizes_Tributaria_IPI()
        {
            data = new List<Matriz_Tributaria_IPI>();
        }

        public Matrizes_Tributaria_IPI(List<Matriz_Tributaria_IPI> data)
        {
            this.data = data;
        }
    }
}