using SAPbobsCOM;
using ServicoF1.Converter.Contract;
using ServicoF1.Models.F1.Pedidos;
using ServicoF1.Models.Misc;
using ServicoF1.Uteis;
using System.Globalization;

namespace ServicoF1.Converter
{
    internal class Parser : IParser<Order, Documents?>, IParsers<Order, Documents, Documents?>, IParsers<Documents, Order, Payments?>
    {
        private ILogger _logger;
        public Parser(ILogger logger)
        {
            _logger = logger;
        }
        public Documents? Parse(Order origin)
        {
            try
            {
                Documents order = (Documents)DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oOrders);
#if DEBUG
                order.CardCode = origin.client_code;
                order.BPL_IDAssignedToInvoice = 1;
#endif
#if RELEASE || RELEASE_PRO
                order.CardCode = origin.client_code;
#endif
                string area_negocio = ParserUteis.GetClientAreaNegocio(origin.client_code);
                if (string.IsNullOrEmpty(area_negocio))
                {
                    _logger.LogError("Não foi possivel achar a area de negócios para o cliente {code}", origin.client_code);
                    return null;
                }

                _logger.LogInformation("area {area}", area_negocio);
                order.PartialSupply = BoYesNoEnum.tNO;
                order.NumAtCard = origin.code;
                order.UserFields.Fields.Item("U_proposta_crm").Value = origin.code;
                order.DocDate = DateTime.Parse(origin.order_date!);
                order.DocDueDate = DateTime.Parse(origin.delivery_date!);
                order.DocCurrency = "R$";
                // caso não haja um cambio para o dia do pedido
                order.DocRate = 1.0;

                //Endereços
                for (int index = 0; index < origin.addresses!.Length; index++)
                {
                    string zip = origin.addresses[index].zipcode!;
                    string type = origin.addresses[index].type == "Entrega" ? "S" : "B";
                    string addrId = ParserUteis.GetAddrIndex(order.CardCode!, zip, type);
                    if (string.IsNullOrEmpty(addrId))
                    {
                        // caso não tenha encontrado um code
                        if (type == "B")
                        {
                            order.AddressExtension.BillToState = origin.addresses[index].state;
                            order.AddressExtension.BillToCity = origin.addresses[index].city;
                            order.AddressExtension.BillToBlock = origin.addresses[index].neighborhood;
                            order.AddressExtension.BillToStreet = origin.addresses[index].street;
                            order.AddressExtension.BillToStreetNo = origin.addresses[index].number;
                            order.AddressExtension.BillToZipCode = origin.addresses[index].zipcode;
                            order.AddressExtension.BillToBuilding = origin.addresses[index].complement;
                            continue;
                        }

                        order.AddressExtension.ShipToState = origin.addresses[index].state;
                        order.AddressExtension.ShipToCity = origin.addresses[index].city;
                        order.AddressExtension.ShipToBlock = origin.addresses[index].neighborhood;
                        order.AddressExtension.ShipToStreet = origin.addresses[index].street;
                        order.AddressExtension.ShipToStreetNo = origin.addresses[index].number;
                        order.AddressExtension.ShipToZipCode = origin.addresses[index].zipcode;
                        order.AddressExtension.ShipToBuilding = origin.addresses[index].complement;
                        continue;
                    }

                    if (type == "S")
                    {
                        order.ShipToCode = addrId;
                        continue;
                    }

                    order.PayToCode = addrId;
                }

                _logger.LogInformation("pay {code}", order.PayToCode);
                _logger.LogInformation("ship {code}", order.ShipToCode);


                //produtos do pedido
                for (int index = 0; index < origin.items!.Length; index++)
                {
                    if (index > 0)
                        order.Lines.Add();
                    order.Lines.ItemCode = origin.items[index].product_code;
                    order.Lines.Quantity = Convert.ToDouble(origin.items[index].qty, new CultureInfo("en-US"));
                    order.Lines.Currency = "R$";
                    order.Lines.CostingCode2 = area_negocio;
                    order.Lines.UnitPrice = Convert.ToDouble(origin.items[index].product_with_discount, new CultureInfo("en-US"));
                    (int? usage, int? deposito) = ParserUteis.GetItemUsage(origin.order_type, order.Lines.ItemCode);
                    if (usage is null || usage == 0)
                        throw new Exception($"Não foram encontrado dados de utilização para o tipo de venda [{Convert.ToInt32(origin.order_type)}] para o item [{order.Lines.ItemCode}] no pedido [{origin.code}]");
                    order.Lines.Usage = usage.ToString();
                    if (deposito is not null && deposito != 0)
                        order.Lines.WarehouseCode = deposito.ToString();

                    _logger.LogInformation("item {item} linha {line}, uso {use}, area {area}", order.Lines.ItemCode, order.Lines.LineNum, order.Lines.Usage, order.Lines.CostingCode2);
                }

                // adição de despesas adicionais(frete)
                double total = 0;
                for (int index = 0; index < origin.distribution_centers!.Count(); index++)
                {
                    var center = origin.distribution_centers![index];
                    total += Convert.ToDouble(center.freight, new CultureInfo("en-US"));
                }

                if (total > 0)
                {
                    order.Expenses.ExpenseCode = 3;
                    order.Expenses.DistributionMethod = BoAdEpnsDistribMethods.aedm_RowTotal;
                    order.Expenses.LineTotal = total;
                    order.TaxExtension.Carrier = ParserUteis.GetCarrier(origin.distribution_centers![0].freight_id_transport);
                    order.UserFields.Fields.Item("U_MMQTpFreteCTR").Value = "2";
                    order.TaxExtension.Incoterms = "1";
                }
                else
                {
                    order.UserFields.Fields.Item("U_MMQTpFreteCTR").Value = "1";
                    order.TaxExtension.Incoterms = "0";
                }

                _logger.LogInformation("transportadora {item} ", order.TaxExtension.Carrier);
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar converter o pedido F1 para um pedido SAP: {erro}", ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return null;
            }
        }
        public Documents? Parse(Order origin, Documents document)
        {

            if (origin.payments is null || origin.payments.Length < 1 || origin.payments[0].internal_type == "BOLETO" || origin.payments[0].internal_type == "postbilledbankbill")
                return null;

            Documents paymentDown = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oDownPayments);
            paymentDown.CardCode = document.CardCode;
            paymentDown.BPL_IDAssignedToInvoice = document.BPL_IDAssignedToInvoice;
            paymentDown.CardName = document.CardName;
            //paymentDown.TaxExtension.MainUsage = document.TaxExtension.MainUsage;
            paymentDown.DocObjectCode = BoObjectTypes.oDownPayments;
            paymentDown.DocType = BoDocumentTypes.dDocument_Items; // tipo deve ser de itens pelo modelo de dados do documento.
            paymentDown.DownPaymentType = DownPaymentTypeEnum.dptInvoice;
            paymentDown.TaxDate = document.TaxDate;
            paymentDown.DocDate = document.DocDate;
            paymentDown.DocDueDate = document.DocDueDate;
            paymentDown.HandWritten = document.HandWritten;
            paymentDown.DownPaymentPercentage = 100.00; // foi posto 100% pois o pagamento será realizado em boleto unico ou cartão de credito.
            paymentDown.DownPaymentStatus = BoSoStatus.so_Closed;

            // copia das linhas do pedido de venda.
            for (int i = 0; i < document.Lines.Count; i++)
            {
                paymentDown.Lines.SetCurrentLine(i);
                document.Lines.SetCurrentLine(i);
                paymentDown.Lines.BaseType = 17;
                paymentDown.Lines.BaseLine = document.Lines.LineNum;
                paymentDown.Lines.BaseEntry = document.DocEntry;
                paymentDown.Lines.Add();
            }

            // logistica
            SAPbobsCOM.AddressExtension extension = paymentDown.AddressExtension;
            paymentDown.Pick = document.Pick;
            paymentDown.LanguageCode = document.LanguageCode;

            // contabilidade
            paymentDown.JournalMemo = document.JournalMemo;
            paymentDown.PaymentMethod = document.PaymentMethod;
            paymentDown.DocTotal = document.DocTotal;
            paymentDown.DiscountPercent = document.DiscountPercent;

            // campos inferiores do pedido de vendas;
            paymentDown.Confirmed = BoYesNoEnum.tYES;

            return paymentDown;
        }
        public Payments? Parse(Documents document, Order origin)
        {
            try
            {
                // tipo de BOLETO, é boleto feito a para pagamento posterior então só ignorar o Invent vai fazer os boletos
                if (origin.payments is null || origin.payments.Length < 1 || origin.payments[0].internal_type == "BOLETO" || origin.payments[0].internal_type == "postbilledbankbill")
                    return null;

                Payments payments = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.oIncomingPayments);
                payments.CardCode = document.CardCode;
                payments.BPLID = document.BPL_IDAssignedToInvoice;
                payments.CardName = document.CardName;
                payments.DocType = BoRcptTypes.rCustomer;
                payments.DueDate = document.DocDueDate;
                payments.DocObjectCode = BoPaymentsObjectType.bopot_IncomingPayments;
                payments.DocDate = document.DocDate;
                payments.BillofExchangeStatus = BoBoeStatus.boes_Closed;
                payments.Invoices.DocEntry = document.DocEntry;
                payments.Invoices.InvoiceType = BoRcptInvTypes.it_DownPayment;

                foreach (var payment in origin.payments!)
                {
                    string type = string.Empty;
                    string account = string.Empty;
                    if (payment.internal_type != "card")
                    {
                        (type, account) = ParserUteis.GetPaymentAccount(payment.internal_type!, document.AddressExtension.BillToCountry.ToUpper() != "BR");
                        if (string.IsNullOrEmpty(account))
                        {
                            _logger.LogError("Não foi encontrado um uma conta para o pagamento {alias}, para o pais {pais}", payment.type, document.AddressExtension.BillToCountry);
                            return null;
                        }
                    }
                    else
                    {
                        type = "C";
                    }

                    switch (type)
                    {
                        case "B":
                            {
                                payments.CashSum = Convert.ToDouble(payment.value, new CultureInfo("en-US"));
                                payments.CashAccount = account;
                                payments.Invoices.SumApplied += Convert.ToDouble(payment.value, new CultureInfo("en-US"));
                            }
                            break;
                        case "P":
                        case "T":
                            {
                                payments.TransferSum = Convert.ToDouble(payment.value, new CultureInfo("en-US"));
                                payments.TransferAccount = account;
                                // por algum motivo sem o ! no final  o billing_date pode ser nulo mesmo eu validando isso como assim
                                payments.TransferDate = DateTime.ParseExact(origin.updated_at is null ? DateTime.Now.ToString() : origin.updated_at[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                                payments.Invoices.SumApplied += Convert.ToDouble(payment.value, new CultureInfo("en-US"));
                            }
                            break;
                        case "C":
                            {
                                payments.CreditCards.Add();
                                /*
                                 * João Copini 23/05/2023
                                 * era para a propriedade payment.type ser usada nos cartões mas a Neoyama
                                 * só vai utilizar de um unico cartão com bandeira REDE no SAP então sera enviado somente
                                 * este cartão, vou manter o metodo por precaução para o futuro
                                 */
                                CartaoCredito? cartao = GetCardCredit("Rede");
                                if (cartao is null)
                                    continue;
                                payments.CreditCards.CreditAcct = cartao.AccountCode;
                                payments.CreditCards.CreditCard = cartao.CardCredit;
                                payments.CreditCards.PaymentMethodCode = int.Parse(document.PaymentMethod); // procura o id do metodo de pagamento com base bandeira do cartão
                                double value = Convert.ToDouble(payment.value, new CultureInfo("en-US"));
                                payments.CreditCards.CreditSum = value;
                                payments.CreditCards.NumOfPayments = payment.installments_qty;
                                // primeiro pagamento
                                payments.CreditCards.FirstPaymentSum = Math.Round(value / payment.installments_qty, 2);
                                double intalmentsValue = payments.CreditCards.FirstPaymentSum;
                                // demais pagamentos
                                for (int index = 1; index < payment.installments_qty; index++)
                                {
                                    if (index == payment.installments_qty - 1)
                                        payments.CreditCards.AdditionalPaymentSum += value - intalmentsValue;
                                    else
                                    {
                                        payments.CreditCards.AdditionalPaymentSum += Math.Round(value / payment.installments_qty, 2);
                                        intalmentsValue += payments.CreditCards.AdditionalPaymentSum;
                                    }
                                }

                                payments.CreditCards.SplitPayments = BoYesNoEnum.tYES;
                                payments.CreditCards.VoucherNum = "123";
                                payments.CreditCards.CreditType = BoRcptCredTypes.cr_InternetTransaction;
                            }
                            break;
                    }
                }

                return payments;
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar gerar o adiantamento F1 para o pedido {} no SAP: {erro}", document.Lines.BaseEntry, ex.Message);
                if (ex.Message.Contains("RPC_E_SERVERFAULT") || ex.Message.Contains("SLD"))
                {
                    DIAPI.API.Reset = true;
                }
                return null;
            }
        }
        private CartaoCredito? GetCardCredit(string? type)
        {
            string query = $"Select AcctCode, CreditCard, CardName from OCRC where CardName like '%{type}%'";
            Recordset recordSet = DIAPI.API.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            recordSet.DoQuery(query);
            if (recordSet.RecordCount > 0)
            {
                return new CartaoCredito(recordSet.Fields.Item("CreditCard").Value,
                    recordSet.Fields.Item("CardName").Value,
                    recordSet.Fields.Item("AcctCode").Value);
            }

            return null;
        }
    }
}