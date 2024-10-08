namespace ServicoF1.Models.F1.Produtos
{
    public sealed class Price
    {
        public string segmentation { get; set; }
        public string value_of { get; set; }
        public string value_for { get; set; }
        public string spot_value { get; set; }
        public string segmentation_name { get; set; }

        public Price()
        {
            segmentation = string.Empty;
            value_of = string.Empty;
            segmentation_name = string.Empty;
            spot_value = string.Empty;
            value_for = string.Empty;
        }
    }
}