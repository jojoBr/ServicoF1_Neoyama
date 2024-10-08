namespace ServicoF1.Models.F1.Pedidos
{
    public class Item
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public int order_distribution_center_id { get; set; }
        public int product_id { get; set; }
        public string? product_code { get; set; }
        public string? product_name { get; set; }
        public string? product_integrated_value_of { get; set; }
        public string? product_integrated_value_for { get; set; }
        public string? product_value { get; set; }
        public string? nature_operation { get; set; }
        public string? product_gross_value { get; set; }
        public string? product_with_discount { get; set; }
        public string? qty { get; set; }
        public string? total_value { get; set; }
        public string? discount { get; set; }
        public string? unitary_custom_discount_value { get; set; }
        public string? total_custom_discount_value { get; set; }
        public string? custom_discount_percent { get; set; }
        public string? unitary_discount_payment_value { get; set; }
        public string? total_discount_payment_value { get; set; }
        public string? unitary_interest_payment_value { get; set; }
        public string? total_interest_payment_value { get; set; }
        public string? unitary_discount_segment_value { get; set; }
        public string? total_discount_segment_value { get; set; }
        public string? segment_discount_percent { get; set; }
        public string? unitary_product_value_with_ipi { get; set; }
        public string? total_product_value_with_ipi { get; set; }
        public string? unitary_product_value_with_icms { get; set; }
        public string? total_product_value_with_icms { get; set; }
        public string? product_without_payment_condition { get; set; }
        public string? product_value_without_payment_condition { get; set; }
        public string? product_total_without_payment_condition { get; set; }
        public string? unitary_product_freight_value { get; set; }
        public string? total_product_freight_value { get; set; }
        public string? unitary_product_freight_value_without_payment_condition { get; set; }
        public string? total_product_freight_value_without_payment_condition { get; set; }
        public string? unitary_st_value { get; set; }
        public string? total_st_value { get; set; }
        public string? unitary_ipi_value { get; set; }
        public string? total_ipi_value { get; set; }
        public string? unitary_icms_value { get; set; }
        public string? total_icms_value { get; set; }
        public string? reduced_icms { get; set; }
        public int price_list_id { get; set; }
        public string? price_list_code { get; set; }
        public string? ipi_aliq { get; set; }
        public string? st_aliq { get; set; }
        public string? fcp_aliq { get; set; }
        public string? extended_warranty_code { get; set; }
        public string? extended_warranty_value { get; set; }
        public string? extended_warranty_unitary_without_payment_condition { get; set; }
        public string? extended_warranty_total_without_payment_condition { get; set; }
        public string? extended_warranty_total { get; set; }
        public string? price_final_sale { get; set; }
        public string? unitary_icms_discount { get; set; }
        public int quantity_canceled { get; set; }
        public object? order_item_status { get; set; }
        public object? message { get; set; }
        public string? progressive_discount_coupon { get; set; }
        public string? updated_for { get; set; }
        public string? created_at { get; set; }
        public string? updated_at { get; set; }
        public object? deleted_at { get; set; }
        public int sequential { get; set; }
        public object? external_item_id { get; set; }
        public string? code_kit { get; set; }
        public int principal_kit { get; set; }
        public string? unitary_product_overprice { get; set; }
        public string? unitary_product_overprice_without_payment_condition { get; set; }
    }

}