namespace ServicoF1.Models.F1.Pedidos
{
    public class Distribution_Centers
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public string? identification { get; set; }
        public string? freight { get; set; }
        public string? freight_original_value { get; set; }
        public string? freight_discount { get; set; }
        public string? freight_delivery_time { get; set; }
        public string? freight_name { get; set; }
        public string? freight_id { get; set; }
        public string? freight_id_transport { get; set; }
        public string? transport_cnpj { get; set; }
        public string? transport_modality { get; set; }
        public string? classification { get; set; }
        public string? code { get; set; }
        public string? state { get; set; }
        public string? invoice_message { get; set; }
        public string? created_at { get; set; }
        public string? updated_at { get; set; }
        public string? deleted_at { get; set; }
    }

}