namespace PureES.Core.Generators.Symbols;

internal interface IAttribute : IToken
{
    public IType Type { get; }

    /// <summary>
    /// Gets the first attribute constructor argument from <c>Type type</c>
    /// </summary>
    public IType TypeParameter { get; }
    
    /// <summary>
    /// Gets the constructor argument from <c>params string[] args</c>
    /// </summary>
    public string[] StringParams { get; }
}