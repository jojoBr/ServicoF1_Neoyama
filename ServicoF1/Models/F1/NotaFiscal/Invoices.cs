namespace ServicoF1.Models.F1.NotaFiscal
{
    public class Invoices
    {
        public Invoice[] data { get; set; }

        public Invoices(Invoice[] invoices)
        {
            data = invoices;
        }
    }
}