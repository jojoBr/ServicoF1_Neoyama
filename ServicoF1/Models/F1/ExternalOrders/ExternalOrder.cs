namespace ServicoF1.Models.F1.ExternalOrders
{
    public class ExternalOrder
    {
        public int? id  { get; set; }
        public string code { get; set; }
        public string client_cpfj { get; set; }
        public string status { get; set; }
        public float total_value { get; set; }
        public float freight_value { get; set; }
        public float taxes_total_value { get; set; }
        public float taxes_ipi_value { get; set; }
        public float taxes_pis_cofins_value { get; set; }
        public float taxes_st_value { get; set; }
        public List<Item> items { get; set; } = new List<Item>();
        public List<Invoice> invoices { get; set; } = new List<Invoice>();
        public Bankbill[] bankbills { get; set; }
    }
}