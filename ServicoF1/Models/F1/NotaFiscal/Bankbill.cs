namespace ServicoF1.Models.F1.NotaFiscal
{
    public class Bankbill
    {
        public string number { get; set; }
        public int value { get; set; }
        public string situation { get; set; }
        public string due_date { get; set; }
        public string url { get; set; }
        public string base64_file { get; set; }
    }

}