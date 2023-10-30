// ReSharper disable ReturnTypeCanBeEnumerable.Global
// ReSharper disable UnusedMember.Global

namespace PureES.Core.SourceGenerators.Symbols;

internal interface IMethod : IToken, IEquatable<IMethod>
{
    public string Name { get; }
    
    /// <summary>
    /// Gets the method return type, or null if <c>void</c>
    /// </summary>
    public IType? ReturnType { get; }

    /// <summary>
    /// Gets the parent class of the method
    /// </summary>
    public IType? DeclaringType { get; }

    public IEnumerable<IParameter> Parameters { get; }

    public IEnumerable<IAttribute> Attributes { get; }

    /// <summary>
    /// Indicates whether the method is public (i.e. can be referenced outside the class)
    /// </summary>
    public bool IsPublic { get; }
    
    
    public bool IsStatic { get; }
}