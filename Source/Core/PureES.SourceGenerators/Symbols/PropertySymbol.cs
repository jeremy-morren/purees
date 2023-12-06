using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace PureES.SourceGenerators.Symbols;

internal class PropertySymbol : IProperty
{
    private readonly IPropertySymbol _source;

    public PropertySymbol(IPropertySymbol source)
    {
        _source = source;
    }

    public Location Location => _source.Locations.GetLocation();

    public string Name => _source.Name;

    public IType Type => new TypeSymbol(_source.Type);

    public bool IsNullable => _source.NullableAnnotation == NullableAnnotation.Annotated;
    
    public bool IsPublic => _source.DeclaredAccessibility == Accessibility.Public;
    public bool IsStatic => _source.IsStatic;
    public bool CanRead => _source.GetMethod != null;
    public bool CanWrite => _source.SetMethod != null;
    public bool IsInitOnly => _source.SetMethod is { IsInitOnly: true };
    public bool IsRequired => _source.IsRequired;

    public IEnumerable<IAttribute> Attributes => _source.GetAttributes()
        .Select(a => new AttributeSymbol(Location, a));
    
    public string? DocumentationXml
    {
        get
        {
            var xml = _source.GetDocumentationCommentXml();
            if (string.IsNullOrEmpty(xml)) return null;
            //Unhelpfully, it comes wrapped in a 'member' node
            try
            {
                var doc = XDocument.Parse(xml);
                var elements = ((XElement)doc.FirstNode).Elements();
                return string.Join(Environment.NewLine, elements.Select(e => e.ToString(SaveOptions.None)));
            }
            catch (XmlException)
            {
                return null;
            }
        }
    }


    public override string ToString() => _source.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    #region Equality
    
    public bool Equals(IProperty? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other is not PropertySymbol o) return false;
        return o._source.Equals(_source, SymbolEqualityComparer.Default);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not PropertySymbol o) return false;
        return o._source.Equals(_source, SymbolEqualityComparer.Default);
    }

    public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(_source);
    
    #endregion
}