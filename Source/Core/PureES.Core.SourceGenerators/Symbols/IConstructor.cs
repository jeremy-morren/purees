namespace PureES.Core.SourceGenerators.Symbols;

internal interface IConstructor : IToken
{
    /// <summary>
    /// Gets the declaring type
    /// (i.e. the type which has this constructor)
    /// </summary>
    IType ContainingType { get; }
    
    IEnumerable<IAttribute> Attributes { get; }
    IEnumerable<IParameter> Parameters { get; }
}