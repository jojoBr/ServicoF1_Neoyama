namespace ServicoF1.Models.F1.ExternalOrders
{
    public class ExternalOrders
    {
        public List<ExternalOrder> data { get; set; }

        public ExternalOrders()
        {
            data = new List<ExternalOrder>();
        }
    }
}