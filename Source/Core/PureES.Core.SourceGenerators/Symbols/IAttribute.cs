namespace PureES.Core.SourceGenerators.Symbols;

internal interface IAttribute : IToken
{
    public IType Type { get; }

    /// <summary>
    /// Gets the first attribute constructor argument as Type
    /// </summary>
    public IType TypeParameter { get; }
    
    /// <summary>
    /// Gets the first attribute constructor argument as string
    /// </summary>
    public string StringParameter { get; }
    
    /// <summary>
    /// Gets the constructor argument from <c>params string[] args</c>
    /// </summary>
    public string[] StringParams { get; }
}