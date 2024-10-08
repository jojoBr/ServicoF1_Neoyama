namespace ServicoF1.Models.F1.Pedidos
{
    public class Address
    {
        public string? number { get; set; }
        public int id { get; set; }
        public int order_id { get; set; }
        public string? type { get; set; }
        public string? state { get; set; }
        public string? city { get; set; }
        public string? neighborhood { get; set; }
        public string? street { get; set; }
        public string? complement { get; set; }
        public string? zipcode { get; set; }
        public string? created_at { get; set; }
        public string? updated_at { get; set; }
        public object? deleted_at { get; set; }
    }
}