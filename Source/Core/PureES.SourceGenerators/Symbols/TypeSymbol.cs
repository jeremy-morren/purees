using Microsoft.CodeAnalysis;

namespace PureES.SourceGenerators.Symbols;

internal class TypeSymbol : IType
{
    private readonly ITypeSymbol _source;
    private readonly bool? _isPartial;

    public TypeSymbol(ITypeSymbol source, 
        bool? isPartial = null)
    {
        _source = source;
        _isPartial = isPartial;
    }

    public Location Location => _source.Locations.GetLocation();

    public IAssembly Assembly => new AssemblySymbol(_source.ContainingAssembly);
    public IType? BaseType => _source.BaseType != null ? new TypeSymbol(_source.BaseType) : null;

    public IType? ContainingType => _source.ContainingType != null ? new TypeSymbol(_source.ContainingType) : null;

    public string? Namespace => _source.ContainingNamespace?.ToString();

    public string Name => GetName(_source);

    public string FullName => _source.ToDisplayString();

    public string GetFullName(bool nullable)
    {
        var type = nullable ? _source : _source.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        return type.ToDisplayString();
    }

    public string CSharpName => _source.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public bool IsInterface => _source.TypeKind == TypeKind.Interface;

    public bool IsReferenceType => _source.IsReferenceType;
    
    public bool IsAbstract => _source.IsAbstract;

    public bool IsPartial => _isPartial ?? throw new NotImplementedException();

    public bool IsGenericType => _source is INamedTypeSymbol { IsGenericType: true };
    
    public bool IsNullable => _source.NullableAnnotation == NullableAnnotation.Annotated;

    public IEnumerable<IAttribute> Attributes => _source.GetAttributes()
        .Select(a => new AttributeSymbol(Location, a));

    public IEnumerable<IConstructor> Constructors => _source.GetMembers()
        .Where(s => s is IMethodSymbol { MethodKind: MethodKind.Constructor })
        .Select(m => new ConstructorSymbol(m));

    public IEnumerable<IProperty> Properties => _source.GetMembers()
        .Where(s => s is IPropertySymbol)
        .Select(p => new PropertySymbol((IPropertySymbol)p));

    public IEnumerable<IMethod> Methods => _source.GetMembers()
        .Where(s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary })
        .Select(s => new MethodSymbol(_source, s));

    public IEnumerable<IType> GenericArguments => ((INamedTypeSymbol)_source).TypeArguments
        .Select(t => new TypeSymbol(t));

    public IEnumerable<IType> ImplementedInterfaces => _source.AllInterfaces
        .Select(t => new TypeSymbol(t));

    public override string ToString() => _source.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static string GetName(ITypeSymbol symbol)
    {
        var name = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return symbol.ContainingType != null
            ? $"{GetName(symbol.ContainingType)}+{name}" 
            : name;
    }

    #region Equality

    public bool Equals(IType? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other is not TypeSymbol o) return false;
        return o._source.Equals(_source, SymbolEqualityComparer.Default);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not TypeSymbol o) return false;
        return o._source.Equals(_source, SymbolEqualityComparer.Default);
    }

    public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(_source);

    #endregion
}