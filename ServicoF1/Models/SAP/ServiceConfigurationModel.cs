namespace ServicoF1.Models.SAP
{
    public class ServiceConfigurationModel
    {
        public string? Token { get; set; }
        public bool ForceTax { get; set; }
        public bool ForceUpdate { get; set; }
        public bool Homolog { get; set; }
        public bool Extra_Log_Data { get; internal set; }
        public bool Test { get; internal set; }
        public string ClientTest { get; internal set; }
    }
}
