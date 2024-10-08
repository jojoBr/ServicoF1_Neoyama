namespace ServicoF1.Models.F1.CRM
{
    public sealed class CRMS
    {
        public List<CRM> data { get; set; }
        public string? next_page_url { get; set; }

        public CRMS()
        {
            data = new List<CRM>();
        }
    }
}