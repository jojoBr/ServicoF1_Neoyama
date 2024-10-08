namespace ServicoF1.Models.F1.ExternalOrders
{
    public class Item
    {
        public string product_code { get; set; }
        public string product_name { get; set; }
        public int quantity { get; set; }
        public float unitary_value { get; set; }
        public float total_value { get; set; }
        public float freight_value { get; set; }
        public float tax_total_value { get; set; }
        public float tax_ipi_value { get; set; }
        public float tax_pis_cofins_value { get; set; }
        public int tax_icms_value { get; set; }
        public float tax_st_value { get; set; }
    }

}