using Microsoft.CodeAnalysis;

namespace PureES.Core.SourceGenerators.Symbols;

internal class AssemblySymbol : IAssembly
{
    private readonly IAssemblySymbol _source;

    public AssemblySymbol(IAssemblySymbol source) => _source = source;

    public string Name => _source.Name;

    public override string ToString() => _source.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    #region Equality
    
    public bool Equals(IAssembly? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other is not AssemblySymbol t) return false;
        return t._source.Equals(_source, SymbolEqualityComparer.Default);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not IAssembly other) return false;
        return Equals(other);
    }

    public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(_source);

    #endregion
}