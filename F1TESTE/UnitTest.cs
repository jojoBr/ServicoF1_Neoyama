using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Newtonsoft.Json.Linq;
using ServicoF1.API;
using ServicoF1.DIAPI;
using ServicoF1.Models.F1.Errors;
using ServicoF1.Models.F1.ProductsCaracteristics.Error;
using ServicoF1.Models.F1.Produtos;
using ServicoF1.Models.WEB;
using ServicoF1.servicos;
using System;
using System.Data.Common;
using System.Text.Json;
using Serilog;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.SystemConsole.Themes;

namespace F1TESTE
{
    [TestClass]
    public sealed class UnitTest
    {
        private readonly string TestToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwOlwvXC9hcGlkYXRhaW4tcHJvZC5mMXdzLmNvbS5iclwvYXBpXC9sb2dpbiIsImlhdCI6MTY4MzI5MjQyMywibmJmIjoxNjgzMjkyNDIzLCJqdGkiOiJibGlFRERocmVmNzE0SkpUIiwic3ViIjoyNDIsInBydiI6Ijg3ZTBhZjFlZjlmZDE1ODEyZmRlYzk3MTUzYTE0ZTBiMDQ3NTQ2YWEifQ.0H96H-tn1tknHbQU5kcvbb5hkGxYycAfq-qsjcweke0";
        private Microsoft.Extensions.Logging.ILogger? logger;
        private Conf conf;

        [TestInitialize]
        public void start()
        {
            var serilog = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
               .Enrich.FromLogContext()
               .WriteTo.Console(theme: AnsiConsoleTheme.Code)
               .CreateLogger();

            var loggerFactory = new LoggerFactory()
            .AddSerilog(serilog);
            logger = loggerFactory.CreateLogger("Logger");
            conf = Conf.GetConfiguration($"{AppDomain.CurrentDomain.BaseDirectory}\\conf.json");
            API.Connect(conf);
        }

        [TestMethod]
        public void TestaUpdateStatusPedido()
        {
            OrderService service = new OrderService(null, TestToken, true, false, "");
            Header[] header = new Header[1] { new Header("Bearer", TestToken) };
            //service.UpdateOrder(header, "f1_1", "RECEIVED", true, null, null);
        }

        [TestMethod]
        public void BuscaClientesParaCadastro()
        {
            CRM_Service service = new CRM_Service(logger, TestToken, false);
            service.CadastraParceirosF1ParaSAP();
        }

        [TestMethod]
        public void CadastraPedidos()
        {
            OrderService service = new ServicoF1.servicos.OrderService(null, TestToken, true, false, "");
            service.Run();
        }

        [TestMethod]
        public void TestaUpdateProdutosDuplicados() 
        {

            string error = Resource.error;
            if (error.Contains("Duplicate entry"))
            {
                error = error.Split("|")[1];
                F1_Error[]? errors = JsonSerializer.Deserialize<F1_Error[]>(error);
                if (errors is null)
                    return;

                for (int index = 0; index < errors.Length; index++)
                {
                    // busca o code do item
                    ReadOnlySpan<char> valores = errors[index].exception_message.AsSpan();
                    int start = valores.IndexOf('\'');
                    string code = "";
                    for (int i = start + 1; i < valores.Length; i++)
                    {
                        if (valores[i].Equals('\''))
                            break;
                        code += valores[i];
                    }

                    Assert.IsTrue(!string.IsNullOrEmpty(code));
                }
            }
        }

        [TestMethod]
        public void TestaBuscaDiasPagamento()
        {
            string val1 = "14,28,42,56,70,84";
            string val2 = "30/45/60/75/90/105/120";
            string val3 = "13 VEZES";
            CRM_Service service = new CRM_Service(null, TestToken, true);
        }

        [TestMethod]
        public void TestaBucarEntryDaStringDeErro()
        {
            string date = DateTime.Now.ToString("yyyyMMdd");
            ReadOnlySpan<char> span = "SQLSTATE[23000]: Integrity constraint violation: 1062 Duplicate entry '50292' for key 'code' ".AsSpan();
            string entry = string.Empty;
            int val = span.IndexOf('\'');
            for (int index = val + 1; index < span.Length; index++)
            {
                char value = span[index];
                if (value == '\'')
                    break;
                entry += value;
            }

            Assert.AreEqual("50292", entry);
        }

        [TestMethod] 
        public void TesteConexao()
        {
            Assert.AreEqual(conf.Token, TestToken);
            var (connected, erro) = API.Connect(conf);
            Assert.IsNotNull(erro);
            Assert.IsTrue(connected);
        }

        [TestMethod]
        public void TestaMetodosDePagamento()
        {
            CRM_Service service = new(null, TestToken, true);
            service.CadastraCondicaoDePagamento(false, true);
        }

        [TestMethod]
        public void CadastraAtributos()
        {
            API.Connect(conf);
            ProductsService service = new ProductsService(null, TestToken, true);
            service.CadastraAtributos();
        }

        [TestMethod]
        public void CadastraItems()
        {
            API.Connect(conf);
            ProductsService service = new ProductsService(null, TestToken, true);
            service.CadastraItems();
        }

        [TestMethod]
        public void UpdateItems()
        {
            API.Connect(conf);
            ProductsService service = new ProductsService(null, TestToken, true);
            service.UpdateItemsToF1(false);
        }

        [TestMethod]
        public void CadastraClasses()
        {
            API.Connect(conf);
            ProductsService service = new ProductsService(null, TestToken, true);
            service.CadastraClasses();
        }

        [TestMethod]
        public void CadastraCategorias()
        {
            API.Connect(conf);
            ProductsService service = new ProductsService(null, TestToken, true);
            service.CadastraCategorias();
        }

        [TestMethod]
        public void CadastraCRM()
        {
            API.Connect(conf);
            CRM_Service service = new CRM_Service(null, TestToken, true);
            service.CadastraCrm(false);
        }

        [TestMethod]
        public void AtualizaCRM()
        {
            API.Connect(conf);
            CRM_Service service = new CRM_Service(null, TestToken, true);
            service.UpdateCrm(false);
        }

        [TestMethod]
        public void CadastraVendedores()
        {
            API.Connect(conf);
            CRM_Service service = new CRM_Service(null, TestToken, true);
            service.CadastraVendedoresECarteiras(false);
        }

        [TestMethod]
        public void CadastraTaxRegime()
        {
            API.Connect(conf);
            TributeService service = new TributeService(null, TestToken, true);
            service.CadastrarRegimesTributarios();
        }

        [TestMethod]
        public void CadastraMatrizIPI()
        {
            //API.Connect(conf);
            TributeService service = new TributeService(null, TestToken, true);
            service.CadastraMatrizesIPI(false, 1, 1, 40);
        }

        [TestMethod]
        public void TestaGetSerializer()
        {
            string value = "[\r\n    {\r\n        \"code\": \"1\",\r\n        \"name\": \"MICRO MOTOR DC\",\r\n        \"parent_class_id\": 8,\r\n        \"updated_at\": \"2022-12-30 11:18:19\",\r\n        \"created_at\": \"2022-12-30 11:18:19\",\r\n        \"id\": 304\r\n    },\r\n    {\r\n        \"code\": \"108\",\r\n        \"name\": \"SERVO MOTOR\",\r\n        \"parent_class_id\": 8,\r\n        \"updated_at\": \"2022-12-30 11:18:19\",\r\n        \"created_at\": \"2022-12-30 11:18:19\",\r\n        \"id\": 305\r\n    },\r\n    {\r\n        \"code\": \"128\",\r\n        \"name\": \"MICRO MOTOR AC\",\r\n        \"parent_class_id\": 8,\r\n        \"updated_at\": \"2022-12-30 11:18:19\",\r\n        \"created_at\": \"2022-12-30 11:18:19\",\r\n        \"id\": 306\r\n    },\r\n    {\r\n        \"code\": \"160\",\r\n        \"name\": \"LEITOR RFID EMBARCADO\",\r\n        \"parent_class_id\": 8,\r\n        \"updated_at\": \"2022-12-30 11:18:20\",\r\n        \"created_at\": \"2022-12-30 11:18:20\",\r\n        \"id\": 307\r\n    },\r\n    {\r\n        \"code\": \"165\",\r\n        \"name\": \"EASY SERVO DRIVER\",\r\n        \"parent_class_id\": 8,\r\n        \"updated_at\": \"2022-12-30 11:18:20\",\r\n        \"created_at\": \"2022-12-30 11:18:20\",\r\n        \"id\": 308\r\n    },\r\n    {\r\n        \"code\": \"260\",\r\n        \"name\": \"CART\\u00c3O\",\r\n        \"parent_class_id\": 8,\r\n        \"updated_at\": \"2022-12-30 11:18:20\",\r\n        \"created_at\": \"2022-12-30 11:18:20\",\r\n        \"id\": 309\r\n    },\r\n    {\r\n        \"code\": \"356\",\r\n        \"name\": \"MOTOR DE PASSO COM ENCODER\",\r\n        \"parent_class_id\": 8,\r\n        \"updated_at\": \"2022-12-30 11:18:20\",\r\n        \"created_at\": \"2022-12-30 11:18:20\",\r\n        \"id\": 310\r\n    },\r\n    {\r\n        \"code\": \"3785\",\r\n        \"name\": \"WEBCAM\",\r\n        \"parent_class_id\": 8,\r\n        \"updated_at\": \"2022-12-30 11:18:21\",\r\n        \"created_at\": \"2022-12-30 11:18:21\",\r\n        \"id\": 311\r\n    },\r\n    {\r\n        \"return\": \"Failed to execute this action\",\r\n        \"success\": false,\r\n        \"exception_message\": \"SQLSTATE[23000]: Integrity constraint violation: 1062 Duplicate entry '50163' for key 'code' \"\r\n    }\r\n]";
            Error[] errors = JsonSerializer.Deserialize<Error[]>(value)!;
            ObjectWithError[] repeated = JsonSerializer.Deserialize<ObjectWithError[]>(value)!;
            Assert.IsTrue(errors.Length > 0);
            Assert.IsTrue(errors.Length > 0);
        }

        [TestMethod]
        public void TestaGet()
        {
            API.Connect(conf);
            ApiConnection conn = new (TestToken, null);
            string url = "http://apidatain.f1ws.com.br/api/products/I000983";
            Header[] header = new Header[1] { new Header("Bearer", TestToken) };
            var item = conn.GET<Response>(url, header).Result;
            Assert.IsNotNull(item);
        }

        [TestMethod]
        public void CadastroDeCliente()
        {
            API.Connect(conf);
            CRM_Service service = new CRM_Service(null, TestToken, true);
            string code = service.CadastrarClienteF1ParaSAP("05950991000197", "").Result;
            Assert.IsNotNull(code);
        }

        [TestMethod]
        public void TestFilaInvoice()
        {
            API.Connect(conf);
            InvoicesService invoices = new(null, TestToken, true);
            invoices.Run();
        }
    }
}