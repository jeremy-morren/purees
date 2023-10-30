

// ReSharper disable ReturnTypeCanBeEnumerable.Global
// ReSharper disable UnusedMember.Global

namespace PureES.Core.SourceGenerators.Symbols;

internal interface IType : IToken, IEquatable<IType> 
{
    public IAssembly Assembly { get; }

    public IType? BaseType { get; }

    /// <summary>
    /// Gets the containing type (for nested class)
    /// </summary>
    public IType? ContainingType { get; }

    public string? Namespace { get; }

    public string Name { get; }

    public string FullName { get; }

    /// <summary>
    /// Gets a fully qualified CSharp type name (ie fully opened generic type),
    /// including 'global::' prefix and nullable qualifier
    /// </summary>
    public string CSharpName { get; }

    public bool IsInterface { get; }

    public bool IsReferenceType { get; }

    public bool IsAbstract { get; }
    
    public bool IsGenericType { get; }

    /// <summary>
    /// Gets all attributes applied to a type
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IAttribute> Attributes { get; }

    /// <summary>
    /// Searches for all public constructors
    /// </summary>
    /// <remarks>
    /// This is the equivalent of  of <c>Type.Constructors(BindingFlags.Public | BindingFlags.Instance)</c>
    /// </remarks>
    public IEnumerable<IConstructor> Constructors { get; }
    
    /// <summary>
    /// Gets all type properties
    /// </summary>
    public IEnumerable<IProperty> Properties { get; }

    /// <summary>
    /// Gets all type methods
    /// </summary>
    public IEnumerable<IMethod> Methods { get; }

    /// <summary>
    /// Gets the generic type parameters if <see cref="IsGenericType"/> is <see langword="true"/>
    /// </summary>
    public IEnumerable<IType> GenericArguments { get; }

    /// <summary>
    /// Gets all interfaces that the type implements (including base types)
    /// </summary>
    public IEnumerable<IType> ImplementedInterfaces { get; }
}