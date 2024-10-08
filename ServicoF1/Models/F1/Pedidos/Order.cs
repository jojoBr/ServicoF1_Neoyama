namespace ServicoF1.Models.F1.Pedidos
{
    public class Order
    {
        public int id { get; set; }
        public string? code { get; set; }
        public int f1_id { get; set; }
        public int enterprise_id { get; set; }
        public string? name_corporate { get; set; }
        public string? name_fancy { get; set; }
        public string? cnpj { get; set; }
        public string? registration_state { get; set; }
        public string? email { get; set; }
        public string? client_code { get; set; }
        public string? representative_name { get; set; }
        public string? representative_email { get; set; }
        public string? representative_phone { get; set; }
        public string? representative_birth_date { get; set; }
        public string? representative_cpf { get; set; }
        public string? representative_cell_phone { get; set; }
        public string? representative_office { get; set; }
        public string order_type { get; set; }
        public int order_sequencial_number { get; set; }
        public string? user_id { get; set; }
        public string? user_code { get; set; }
        public string? tele_sales_user_code { get; set; }
        public string? link { get; set; }
        public string? origin_ip { get; set; }
        public string? phone { get; set; }
        public string? order_date { get; set; }
        public string? delivery_date { get; set; }
        public string? exportation_date { get; set; }
        public string? status { get; set; }
        public string? status_id { get; set; }
        public object? status_message { get; set; }
        public string? external_status_id { get; set; }
        public string? modification_date { get; set; }
        public int partial_billing { get; set; }
        public string? order_total_value { get; set; }
        public string? order_total_st { get; set; }
        public string? order_total_ipi { get; set; }
        public string? order_total_discounts { get; set; }
        public string? order_freight { get; set; }
        public string? client_comment { get; set; }
        public object? client_segment_external_code { get; set; }
        public string? tax_discount { get; set; }
        public string? purchase_order { get; set; }
        public object? approved_date { get; set; }
        public string? total_extended_warranty { get; set; }
        public int? responsible_order_user_id { get; set; }
        public string? responsible_order_user_type { get; set; }
        public string? responsible_order_user_name { get; set; }
        public string? responsible_order_user_mail { get; set; }
        public object? responsible_order_user_external_code { get; set; }
        public object? responsible_order_user_cellphone { get; set; }
        public object? responsible_order_client_external_code { get; set; }
        public object? responsible_order_user_code { get; set; }
        public object? responsible_order_user_cnpj { get; set; }
        public string? invoice_message { get; set; }
        public object? billing_date { get; set; }
        public int receive_delivery_information { get; set; }
        public object? sub_external_code { get; set; }
        public object? reservation_id { get; set; }
        public string? created_at { get; set; }
        public string? updated_at { get; set; }
        public object? deleted_at { get; set; }
        public string? updated_for { get; set; }
        public Address[]? addresses { get; set; }
        public Distribution_Centers[]? distribution_centers { get; set; }
        public Payment[]? payments { get; set; }
        public Payments_Providers[]? payments_providers { get; set; }
        public Item[]? items { get; set; }
        public object[]? invoices { get; set; }
    }

}