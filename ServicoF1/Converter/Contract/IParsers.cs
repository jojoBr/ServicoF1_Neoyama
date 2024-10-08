namespace ServicoF1.Converter.Contract
{
    internal interface IParsers<O, P, D>
    {
        D Parse(O origin, P pedido);
    }
}