using SAPbobsCOM;

namespace ServicoF1.DIAPI
{
    public static class API
    {
#pragma warning disable CS8618 // O campo não anulável precisa conter um valor não nulo ao sair do construtor. Considere declará-lo como anulável.
        public static Company Company { get; set; }
        public static bool Conected { get; set; }
        public static bool Reset { get; set; }
#pragma warning restore CS8618 // O campo não anulável precisa conter um valor não nulo ao sair do construtor. Considere declará-lo como anulável.

        /// <summary>
        /// metodod que Faz a conexão com o B1.
        /// </summary>
        /// <param name="conf"> Classe de parametros de conexão. </param>
        /// <returns>connect as bool and erro if not connected </returns>
        public static (bool, string) Connect(Conf conf)
        {
            // parametros de conexão
            string erro = string.Empty;
            Company = new Company();
            Company.LicenseServer = conf.LicenceServer;
            Company.CompanyDB = conf.CompanyDB;
            Company.UserName = conf.UserName;
            Company.Password = conf.Password;
            Company.DbServerType = (BoDataServerTypes)conf.DBType;
            Company.SLDServer = conf.SLDServer;
            Company.Server = conf.Server;
            Company.DbUserName = conf.UserDB;
            Company.DbPassword = conf.PassDB;
            Company.language = BoSuppLangs.ln_Portuguese_Br;
            Company.UseTrusted = false;

            if (Company.Connect() != 0)
            {
                erro = $"{Company.GetLastErrorCode()} | {Company.GetLastErrorDescription()}";
                return (false, erro);
            }

            Conected = true;
            return (true, "");
        }

        public static void ResetAPI()
        {
            try
            {
                // tenta desconectar a empresa
                Company.Disconnect();
                Reset = false;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                GC.Collect();
                Conf conf = Conf.GetConfiguration($"{AppDomain.CurrentDomain.BaseDirectory}\\conf.json");
                Connect(conf);
            }
        }
    }
}