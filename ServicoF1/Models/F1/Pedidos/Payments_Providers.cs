namespace ServicoF1.Models.F1.Pedidos
{
    public class Payments_Providers
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public object? message { get; set; }
        public object? tid { get; set; }
        public object? nsu { get; set; }
        public object? authorization_code { get; set; }
        public object? payment_id { get; set; }
        public object? receive_date { get; set; }
        public object? provider { get; set; }
        public int installments { get; set; }
        public object? proof_of_sale { get; set; }
        public object? card_number { get; set; }
        public object? card_bin { get; set; }
        public object? card_end { get; set; }
        public string? created_at { get; set; }
        public string? updated_at { get; set; }
        public object? deleted_at { get; set; }
    }

}