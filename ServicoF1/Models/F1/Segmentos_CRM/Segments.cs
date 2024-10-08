namespace ServicoF1.Models.F1.Segmentos_CRM
{
    public sealed class Segments
    {
        public string code { get; set; }
        public string external_id { get; set; }
        public string external_code { get; set; }
        public string name { get; set; }
        public string minimun_value { get; set; }
        public string price_list { get; set; }
        public string[] crm { get; set; }

        public Segments(int crmCount = 0)
        {
            code = string.Empty;
            external_id = string.Empty;
            name = string.Empty;
            minimun_value = string.Empty;
            price_list = string.Empty;
            external_code = string.Empty;
            crm = new string[crmCount];
        }
    }
}