namespace PureES.Core.Generators.Symbols;

internal interface IProperty : IToken, IEquatable<IProperty>
{
    string Name { get; }

    IType Type { get; }

    IEnumerable<IAttribute> Attributes { get; }

    bool IsNullable { get; }

    bool IsPublic { get; }

    bool IsStatic { get; }

    bool CanRead { get; }

    bool CanWrite { get; }

    bool IsInitOnly { get; }

    /// <summary>
    /// Whether the property has the <c>required</c> keyword
    /// </summary>
    bool IsRequired { get; }

    /// <summary>
    /// Gets the XML documentation
    /// </summary>
    string? DocumentationXml { get; }
}