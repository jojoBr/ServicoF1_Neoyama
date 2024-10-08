namespace ServicoF1.Models.F1.Pedidos
{
    public class Payment
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public string? external_code { get; set; }
        public string? value { get; set; }
        public string? discount { get; set; }
        public string? discount_total { get; set; }
        public string? fees { get; set; }
        public string? comments { get; set; }
        public string? type { get; set; }
        public int installments_qty { get; set; }
        public string? installment { get; set; }
        public string? internal_type { get; set; }
        public string? created_at { get; set; }
        public string? updated_at { get; set; }
        public object? deleted_at { get; set; }
    }

}