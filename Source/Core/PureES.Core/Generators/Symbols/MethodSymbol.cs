using Microsoft.CodeAnalysis;

namespace PureES.Core.Generators.Symbols;

internal class MethodSymbol : IMethod
{
    private readonly ITypeSymbol? _declaringType;
    private readonly IMethodSymbol _source;
    
    public MethodSymbol(ITypeSymbol? declaringType, ISymbol symbol)
    {
        if (symbol is not IMethodSymbol m)
            throw new ArgumentOutOfRangeException(nameof(symbol));
        _declaringType = declaringType;
        _source = m;
    }

    public Location Location => _source.Locations.GetLocation();
    public string Name => _source.Name;
    public IType? ReturnType => _source.ReturnsVoid ? null : new TypeSymbol(_source.ReturnType);
    public IType? DeclaringType => _declaringType != null ? new TypeSymbol(_declaringType) : null;

    public IEnumerable<IParameter> Parameters => _source.Parameters
        .Select(p => new ParameterSymbol(p));

    public IEnumerable<IAttribute> Attributes => _source.GetAttributes()
        .Where(a => a.AttributeClass != null)
        .Select(a => new AttributeSymbol(Location, a));

    public bool IsPublic => _source.DeclaredAccessibility is 
        Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    public bool IsStatic => _source.IsStatic;

    public override string ToString() => _source.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    
    #region Equality

    public bool Equals(IMethod? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return other is MethodSymbol o && _source.Equals(o._source, SymbolEqualityComparer.Default);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is MethodSymbol o && _source.Equals(o._source, SymbolEqualityComparer.Default);
    }

    public override int GetHashCode()
    {
        return SymbolEqualityComparer.Default.GetHashCode(_source);
    }
    
    #endregion
}