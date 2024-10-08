using static System.Net.Mime.MediaTypeNames;

namespace ServicoF1.servicos
{
    public sealed class Startup
    {

        public ILogger<Worker> logger;

        public Startup(ILogger<Worker> logger)
        {
            this.logger = logger;
        }

        /*
         * workflow do serviço de acordo com inspeção da documentação sujeito a mudanças
         * 1.Cadastro de atributos ok
         * 2.Cadastro de classes ok
         * 3.Cadastro de categorias indefinido no momento talvez seja desnecessário ok
         * 4.Cadastro de lista de preços ok, cadastrado junto do item ok
         * 5.Cadastro de produtos e seu estoque inicial ok, cadastrado junto do item ok
         * 6.Cadastro de regime de imposto
         * 7.Cadastro de matriz tributaria
         * 8.Cadastro de parceiros do SAP para o F1 ok
         * 8.1.Cadastro de vendedores e carteiras deve ser feito antes do cadastro ok
         * 8.2.Cadastro de segmentos, precisa ser estabelecido os segmentos primeiro e depois cadastrado os segmentos após o cadastro dos parceiros
         * 9.Cadastrar pedidos que vem do F1 para o SAP
         * 10.Atualizar os dados dos pedidos do SAP para o F1
         * 11.Atualizar a nota fiscal do pedido quando atualizada no SAP
         */
        public void RunService(string token, bool update, bool test, string client_test, bool DataSendIntoLogger, int compid, bool homolog = false)
        {
            try
            {
                //Services
                ProductsService productsService = new(logger, token, homolog);
                CRM_Service cRM_Service = new(logger, token, homolog);
                OrderService orderService = new(logger, token, homolog, test, client_test);
                InvoicesService invoicesService = new(logger, token, homolog, compid);
                ExternalOrdersService externalOrdersService = new(token, homolog, logger);
                //Threads
                Thread threadProdutc = new(() => productsService.Run(update, DataSendIntoLogger));
                Thread threadCRM = new(() => cRM_Service.Run(DataSendIntoLogger, update));
                Thread Orders = new(() => orderService.Run());
                Thread threadInvoice = new(() => invoicesService.Run());
                Thread threadexternalOrders = new(() => externalOrdersService.Run(DataSendIntoLogger));

                threadProdutc.Start();
                threadCRM.Start();
                Orders.Start();
                threadInvoice.Start();
                threadexternalOrders.Start();

                threadProdutc.Join();
                threadCRM.Join();
                Orders.Join();
                threadInvoice.Join();
                threadexternalOrders.Join();
            }
            catch (Exception ex)
            {
                logger.LogError("Erro ao rodar serviço de produtos: {erro}", ex.Message);
            }
            finally
            {
                GC.Collect();
            }
        }

        public void RunTributeService(string token, bool DataSendIntoLogger)
        {
            try
            {
                TributeService tributeService = new(logger, token);
                tributeService.Run(DataSendIntoLogger);
            }
            catch (Exception ex)
            {
                logger.LogError("Erro ao rodar serviço de Tributos: {erro}", ex.Message);
            }
        }
    }
}