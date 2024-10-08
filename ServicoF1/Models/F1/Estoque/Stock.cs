namespace ServicoF1.Models.F1.Estoque
{
    public sealed class Stock
    {
        public string segmentation { get; set; }
        public string qty_reservation { get; set; }
        public string qty_stock { get; set; }
        public string ressuply_deadline { get; set; }

        public Stock()
        {
            segmentation = string.Empty;
            qty_reservation = string.Empty;
            qty_stock = string.Empty;
            ressuply_deadline = string.Empty;
        }
    }
}