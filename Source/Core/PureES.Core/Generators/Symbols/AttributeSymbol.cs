using Microsoft.CodeAnalysis;

namespace PureES.Core.Generators.Symbols;

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

    public IType TypeParameter
    {
        get
        {
            
            if (_source.ConstructorArguments.Length == 0) 
                throw new Exception("Attribute has no constructor arguments");

            var arg = _source.ConstructorArguments[0];
            if (arg.Value is not ITypeSymbol source)
                throw new ArgumentException("Invalid attribute parameter");
            return new TypeSymbol(source);
        }
    }
    
    public string[] StringParams
    {
        get
        {
            
            if (_source.ConstructorArguments.Length == 0) 
                throw new Exception("Attribute has no constructor arguments");

            var arg = _source.ConstructorArguments[0];
            if (arg.Kind != TypedConstantKind.Array)
                throw new ArgumentException("Invalid attribute parameter");
            return arg.Values.Select(v => (string)v.Value!).ToArray();
        }
    }

    public override string ToString() => _source.AttributeClass?.ToDisplayString() ?? _source.ToString();
}