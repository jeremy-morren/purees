using Microsoft.CodeAnalysis;

namespace PureES.SourceGenerators.Symbols;

internal class ConstructorSymbol : IConstructor
{
    private readonly IMethodSymbol _source;

    public ConstructorSymbol(ISymbol source)
    {
        if (source is not IMethodSymbol { MethodKind: MethodKind.Constructor } m)
            throw new ArgumentOutOfRangeException(nameof(source));
        _source = m;
    }

    public Location Location => _source.Locations.GetLocation();
    
    public IType ContainingType => new TypeSymbol(_source.ContainingType);

    public IEnumerable<IAttribute> Attributes => _source.GetAttributes()
        .Where(a => a.AttributeClass != null)
        .Select(a => new AttributeSymbol(Location, a));

    public IEnumerable<IParameter> Parameters => _source.Parameters
        .Select(p => new ParameterSymbol(p));

    public override string ToString() => _source.ToDisplayString();
}