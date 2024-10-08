using ServicoF1.servicos;
using System.Text.Json;

namespace ServicoF1.DIAPI
{
    public struct Conf
    {
        public string Server { get; set; }
        public string LicenceServer { get; set; }
        public string SLDServer { get; set; }
        public string CompanyDB { get; set; }
        public int DBType { get; set; }
        public string UserDB { get; set; }
        public string PassDB { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Token { get; set; }
        public int CompId { get; set; }
        public bool? ForceTaxAddition { get; set; }
        public bool? ForceUpdate { get; set; }
        public bool? SendDataIntoToLogger { get; set; }
        public bool? Test { get; set; }
        public bool? Homolog { get; set; }
        public string? Client_test { get; set; }

        public static Conf GetConfiguration(string path, bool disableForceTaxAfterSearch = false)
        {
            using (StreamReader reader = new(path))
            {
                string json = reader.ReadToEnd();
                Conf conf =  JsonSerializer.Deserialize<Conf>(json)!;
                reader.Close();
                Conf decrypted = DecryptedConf(conf);
                if (disableForceTaxAfterSearch)
                {
                    conf.ForceTaxAddition = null;
                    string value = JsonSerializer.Serialize(conf);
                    File.WriteAllText(path, value);
                }
                return decrypted;
            }
        }

        private static Conf DecryptedConf(Conf conf)
        {
            return new Conf()
            {
                Server = Security.Decrypt(conf.Server)!,
                LicenceServer = Security.Decrypt(conf.LicenceServer)!,
                SLDServer = Security.Decrypt(conf.SLDServer)!,
                CompanyDB = Security.Decrypt(conf.CompanyDB)!,
                DBType = conf.DBType,
                UserDB = Security.Decrypt(conf.UserDB)!,
                PassDB = Security.Decrypt(conf.PassDB)!,
                UserName = Security.Decrypt(conf.UserName)!,
                Password = Security.Decrypt(conf.Password)!,
                Token = Security.Decrypt(conf.Token)!,
                CompId = conf.CompId,
                ForceTaxAddition = conf.ForceTaxAddition is null ? false : conf.ForceTaxAddition,
                ForceUpdate = conf.ForceUpdate is null ? false : conf.ForceUpdate,
                SendDataIntoToLogger = conf.SendDataIntoToLogger is null ? false : conf.SendDataIntoToLogger,
                Test = conf.Test is null ? false : conf.Test,
                Client_test = conf.Client_test is null ? "" : conf.Client_test,
                Homolog = conf.Homolog is null ? false : conf.Homolog,
            };
        }
    }
}