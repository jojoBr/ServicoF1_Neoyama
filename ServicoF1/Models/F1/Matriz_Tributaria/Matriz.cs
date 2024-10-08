namespace ServicoF1.Models.F1.Matriz_Tributaria
{
    public class Matriz
    {
        public string code { get; set; }
        public string external_order_type { get; set; }
        public string ncm { get; set; }
        public string tax_regime { get; set; }
        public string home_state { get; set; }
        public string destination_state { get; set; }
        public string icms_type { get; set; }
        public float intern_icms { get; set; }
        public float extern_icms { get; set; }
        public string product_origin { get; set; }
        public float st { get; set; }
        public string item_code { get; set; }
        public string client_cnpj { get; set; }
        public float icms_internal_destination { get; set; }
        public int icms_reduction { get; set; }
        public int fcp_st { get; set; }
        public int fcp_base { get; set; }
        public string sell_type { get; set; }
        public string nature_operation { get; set; }
        public string city_destination { get; set; }

        public Matriz()
        {
            code = string.Empty;
            external_order_type = string.Empty;
            ncm = string.Empty;
            tax_regime = string.Empty;
            home_state = string.Empty;
            destination_state = string.Empty;
            icms_type = string.Empty;
            item_code = string.Empty;
            client_cnpj = string.Empty;
            sell_type = string.Empty;
            nature_operation = string.Empty;
            city_destination = string.Empty;
        }
    }
}
