namespace ServicoF1.Models.Misc
{
    internal class CartaoCredito
    {
        /// <summary>
        /// Gets or sets id do cartão.
        /// </summary>
        public int CardCredit { get; set; }

        /// <summary>
        /// Gets or sets nome do cartão.
        /// </summary>
        public string CardName { get; set; }

        /// <summary>
        /// Gets or sets numero da conta.
        /// </summary>
        public string AccountCode { get; set; }

        public CartaoCredito(int cardCredit, string cardName, string accountCode)
        {
            CardCredit = cardCredit;
            CardName = cardName;
            AccountCode = accountCode;
        }
    }
}
