namespace ServicoF1.Models.F1.Condicao_de_Pagamento
{
    public class Condicao
    {
        public string code { get; set; }
        public int? id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int installments_number { get; set; }
        public string order_completion_text { get; set; }
        public int status { get; set; }
        public List<Installment> installments { get; set; }
    }

}
