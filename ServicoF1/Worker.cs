using Serilog;
using ServicoF1.API;
using ServicoF1.DIAPI;
using ServicoF1.servicos;
using System.Runtime.InteropServices;

namespace ServicoF1
{
    public sealed class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly Startup _startup;
        private const int TIME_TO_ENTER_UPDATE = 3;
        public Worker(ILogger<Worker> logger, Startup startup)
        {
            _logger = logger;
            _startup = startup;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int count = 0;
            bool taxDone = false;
            Client._logger = _logger;
            Conf conf = Conf.GetConfiguration($"{AppDomain.CurrentDomain.BaseDirectory}\\conf.json");
            (bool conected, string erro) = DIAPI.API.Connect(conf);
            if (!conected)
            {
                _logger.LogError("Erro ao iniciar o serviço, dados de acesso errado por favor altere e tente novamente iniciar o serviço:{error}", erro);
                return;
            }
            else
            {
                _logger.LogInformation("Sucesso ao conectar a base");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (DIAPI.API.Reset)
                        DIAPI.API.ResetAPI();

                    //versao 9
                    Conf tempConfs = Conf.GetConfiguration($"{AppDomain.CurrentDomain.BaseDirectory}\\conf.json");
                    if (string.IsNullOrEmpty(tempConfs.Token))
                    {
                        _logger.LogError("Não foi possivel encontrar uma configuração ou token");
                        return;
                    }

                    _logger.LogInformation("dados de configuração pesquisados com sucesso");
                    if ((DateTime.Now.Hour >= 1 && DateTime.Now.Hour < 2 && !taxDone) || (bool)tempConfs.ForceTaxAddition!)
                    {
                        TributeFunctions(tempConfs.Token, (bool)tempConfs.SendDataIntoToLogger!, stoppingToken);
                        taxDone = true;
                    }
                    else
                    {
                        // controla se deve atualizar as funções de atualizar ao inves de inserir dados e caso o stock deva ser atualizado
                        bool update = count == TIME_TO_ENTER_UPDATE || (bool)tempConfs.ForceUpdate!;
                        PrimaryFunctions(tempConfs.Token, update, (bool)tempConfs.Test!, tempConfs.Client_test!, (bool)tempConfs.SendDataIntoToLogger!, (bool)tempConfs.Homolog!, tempConfs.CompId, stoppingToken);
                        count++;
                        if (count > TIME_TO_ENTER_UPDATE)
                            count = 0;
                    }

                    //versao 10
                    //var serviceconf = ServiceConfiguration.GetServiceConfiguration(_logger);
                    //if (serviceconf is null || string.IsNullOrEmpty(serviceconf.Token))
                    //{
                    //    _logger.LogError("Não foi possivel encontrar uma configuração ou token");
                    //    return;
                    //}

                    //_logger.LogInformation("dados de configuração pesquisados com sucesso");
                    //if ((DateTime.Now.Hour >= 1 && DateTime.Now.Hour < 2 && !taxDone) || serviceconf.ForceTax)
                    //{
                    //    TributeFunctions(serviceconf.Token, serviceconf.Extra_Log_Data, stoppingToken);
                    //    taxDone = true;
                    //}
                    //else
                    //{
                    //    // controla se deve atualizar as funções de atualizar ao inves de inserir dados e caso o stock deva ser atualizado
                    //    bool update = count == TIME_TO_ENTER_UPDATE || serviceconf.ForceUpdate;
                    //    PrimaryFunctions(serviceconf.Token, update, serviceconf.Test, serviceconf.ClientTest!, serviceconf.Extra_Log_Data, serviceconf.Homolog, stoppingToken);
                    //    count++;
                    //    if (count > TIME_TO_ENTER_UPDATE)
                    //        count = 0;
                    //}
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao iniciar o serviço {error}", ex.Message);
                    if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                    {
                        DIAPI.API.Reset = true;
                    }
                }
                finally
                {
                    if (DateTime.Now.Hour < 1 || DateTime.Now.Hour > 2)
                        taxDone = false;
                    await Task.Delay(TimeSpan.FromMinutes(7), stoppingToken);
                }
            }

            // desconecta di api ao desligar o serviço
            if (DIAPI.API.Company is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && DIAPI.API.Conected)
            {
                DIAPI.API.Company.Disconnect();
                Marshal.FinalReleaseComObject(DIAPI.API.Company);
                GC.Collect();
            }

            Client.Dispose();
        }

        private void TributeFunctions(string token, bool DataSendIntoLogger, CancellationToken stoppingToken)
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Rodando dados Tributários : {time}", DateTimeOffset.Now);
                    _startup.RunTributeService(token, DataSendIntoLogger);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao rodar o serviço primario {error}", ex.Message);
                    if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                    {
                        DIAPI.API.Reset = true;
                    }
                }
            }
        }

        private void PrimaryFunctions(string token, bool update, bool test, string client_test, bool DataSendIntoLogger, bool homolog, int compId, CancellationToken stoppingToken)
        {
            if(!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    string metodo = update ? "Atualizando" : "Cadastrando";
                    _logger.LogInformation("{metodo} dados : {time}", metodo, DateTimeOffset.Now);
                   _startup.RunService(token, update, test, client_test, DataSendIntoLogger, compId, homolog);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao rodar o serviço primario {error}", ex.Message);
                    if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                    {
                        DIAPI.API.Reset = true;
                    }
                }
            }
        }

        public override void Dispose()
        {
            Log.CloseAndFlush();
            base.Dispose();
        }
    }
}