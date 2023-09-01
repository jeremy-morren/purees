namespace PureES.Core.Generators.Symbols;

internal interface IParameter : IToken
{
    public string Name { get; }
    
    public IType Type { get; }

    IEnumerable<IAttribute> Attributes { get; }
}