namespace ServicoF1.Converter.Contract
{
    internal interface IParser<O, D>
    {
        D Parse(O origin);
    }
}