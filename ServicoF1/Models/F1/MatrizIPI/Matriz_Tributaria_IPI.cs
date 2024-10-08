namespace ServicoF1.Models.F1.MatrizIPI
{
    public class Matriz_Tributaria_IPI
    {
        public string? code { get; set; }
        public string item_code { get; set; }
        public string ncm { get; set; }
        public string client_cnpj { get; set; }
        public string city_destination { get; set; }
        public string home_state { get; set; }
        public string destination_state { get; set; }
        public string product_origin { get; set; }
        public string tax_regime { get; set; }
        public string sell_type { get; set; }
        public string nature_operation { get; set; }
        public string external_order_type { get; set; }
        public string ipi { get; set; }


        public Matriz_Tributaria_IPI()
        {
            item_code = "*";
            ncm = "*";
            client_cnpj = "*";
            city_destination = "*";
            home_state = "*";
            destination_state = "*";
            product_origin = "*";
            tax_regime = "*";
            sell_type = "*";
            nature_operation = "*";
            ipi = "*";
        }
    }
}