using SAPbobsCOM;
using ServicoF1.Models.SAP;
using System.Runtime.InteropServices;

namespace ServicoF1.servicos
{
    public static class ServiceConfiguration
    {
        public static ServiceConfigurationModel? GetServiceConfiguration(ILogger logger)
        {
            Recordset? recordset = null;
            try
            {
                recordset = (Recordset)DIAPI.API.Company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
                string query = @"SELECT TOP 1 ""U_Token"", ""U_ForceTax"", ""U_ForceUpdate"", ""U_Homolog"", ""U_ExtraLogData"",  ""U_Test"",  ""U_ClientTest"" FROM ""@F1_CONFIGURATION""";
                recordset.DoQuery(query);
                if (recordset.RecordCount > 0)
                {
                    logger.LogError("Configuração e token Encontrada");
                    return new ServiceConfigurationModel()
                    {
                        Token = Convert.ToString(recordset.Fields.Item(0).Value),
                        ForceTax = ((string)recordset.Fields.Item(1).Value) == "Y",
                        ForceUpdate = ((string)recordset.Fields.Item(2).Value) == "Y",
                        Homolog = ((string)recordset.Fields.Item(3).Value) == "Y",
                        Extra_Log_Data = ((string)recordset.Fields.Item(4).Value) == "Y",
                        Test = ((string)recordset.Fields.Item(5).Value) == "Y",
                        ClientTest = Convert.ToString(recordset.Fields.Item(6).Value),
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.LogError("Erro ao buscar a configuração {erro}", ex.Message);
                return null;
            }
            finally
            {
                if (recordset is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Marshal.ReleaseComObject(recordset);
            }
        }
    }
}
