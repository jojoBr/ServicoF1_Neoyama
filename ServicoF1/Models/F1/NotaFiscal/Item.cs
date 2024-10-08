namespace ServicoF1.Models.F1.NotaFiscal
{
    public class Item
    {
        public string product_code { get; set; }
        public string attended_quantity { get; set; }

        public Item(string product_code, string attended_quantity)
        {
            this.product_code = product_code;
            this.attended_quantity = attended_quantity;
        }
    }

}