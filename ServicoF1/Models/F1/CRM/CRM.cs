namespace ServicoF1.Models.F1.CRM
{
    public sealed class CRM
    {
        public string? cgc { get; set; }
        public string? code { get; set; }
        public string? person_type { get; set; }
        public string? tax_regime { get; set; }
        public string? name_fancy { get; set; }
        public string? name_corporate { get; set; }
        public string? registration_state { get; set; }
        public string? registration_municipal { get; set; }
        public string? suframa_code { get; set; }
        public string? suframa_expiration_date { get; set; }
        public string? phone_number { get; set; }
        public string? cell_phone_number { get; set; }
        public string? email { get; set; }
        public string? email_invoice { get; set; }
        public string? external_segment_code { get; set; }
        public string? price_list { get; set; }
        public double? credit { get; set; }
        public object? average_payment_period { get; set; }
        public int? allow_post_payment { get; set; }
        public object? default_order_type { get; set; }
        public string? default_operation_type { get; set; }
        public object? exclusive_payment_method_condition_installment { get; set; }
        public object? simples_code { get; set; }
        public int moderated { get; set; }
        public Wallet[]? wallets { get; set; }
        public Customfield[]? customfields { get; set; }
        public Odertypes[] ordertypes { get; set; }
        public Concept[]? concepts { get; set; }
        public string? created { get; set; }
        public string? address_street { get; set; }
        public string? address_number { get; set; }
        public string? address_complement { get; set; }
        public string? address_state { get; set; }
        public string? address_city { get; set; }
        public string? address_neighborhood { get; set; }
        public string? address_country { get; set; }
        public string? address_reference { get; set; }
        public string? address_zipcode { get; set; }
        public string? delivery_address_street { get; set; }
        public string? delivery_address_number { get; set; }
        public string? delivery_address_complement { get; set; }
        public string? delivery_address_state { get; set; }
        public string? delivery_address_city { get; set; }
        public string? delivery_address_neighborhood { get; set; }
        public string? delivery_address_country { get; set; }
        public string? delivery_address_reference { get; set; }
        public string? delivery_address_zipcode { get; set; }
        public string? charge_address_street { get; set; }
        public string? charge_address_number { get; set; }
        public string? charge_address_complement { get; set; }
        public string? charge_address_state { get; set; }
        public string? charge_address_city { get; set; }
        public string? charge_address_neighborhood { get; set; }
        public string? charge_address_country { get; set; }
        public string? charge_address_reference { get; set; }
        public string? charge_address_zipcode { get; set; }
        public int active { get; set; }
        public int? id  { get; set; }
    }
}