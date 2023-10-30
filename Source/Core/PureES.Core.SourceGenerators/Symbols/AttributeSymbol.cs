using Microsoft.CodeAnalysis;

namespace PureES.Core.SourceGenerators.Symbols;

internal class AttributeSymbol : IAttribute
{
    private readonly AttributeData _source;

    public AttributeSymbol(Location location, AttributeData source)
    {
        Location = location;
        
        _source = source;
    }

    public Location Location { get; }
    
    public IType Type => new TypeSymbol(
        _source.AttributeClass ?? throw new Exception("Attribute class is null"));

    private T GetFirstParam<T>(Func<TypedConstant, T> selector)
    {
        
        if (_source.ConstructorArguments.Length == 0) 
            throw new Exception("Attribute has no constructor arguments");

        var arg = _source.ConstructorArguments[0];
        return selector(arg);
    }

    public IType TypeParameter => GetFirstParam(arg =>
    {
        if (arg.Value is not ITypeSymbol source)
            throw new ArgumentException("Invalid attribute parameter");
        return new TypeSymbol(source);
    });

    public string StringParameter => GetFirstParam(arg =>
    {
        if (arg.Value is not string str)
            throw new AggregateException("Invalid attribute parameter");
        return str;
    });

    public string[] StringParams => GetFirstParam(arg =>
    {
        if (arg.Kind != TypedConstantKind.Array)
            throw new ArgumentException("Invalid attribute parameter");
        return arg.Values.Select(v => (string)v.Value!).ToArray();
    });

    public override string ToString() => _source.AttributeClass?.ToDisplayString() ?? _source.ToString();
}