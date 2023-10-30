namespace PureES.Core.SourceGenerators.Generators;

internal static class AttributeHelpers
{
    private static bool Is(this IAttribute attribute, string name) => 
        attribute.Type.Name.Equals(name, StringComparison.Ordinal);
    
    public static bool Is<TAttribute>(this IAttribute attribute) => attribute.Is(typeof(TAttribute).Name);

    public static bool IsFromServicesAttribute(this IAttribute attribute) => attribute.Is("FromServicesAttribute");

    public static bool HasAttribute<TAttribute>(this IParameter parameter) => parameter.Attributes.Any(Is<TAttribute>);
    public static bool HasAttribute<TAttribute>(this IType type) => type.Attributes.Any(Is<TAttribute>);
    public static bool HasAttribute<TAttribute>(this IMethod method) => method.Attributes.Any(Is<TAttribute>);

    public static bool HasFromServicesAttribute(this IParameter parameter) =>
        parameter.Attributes.Any(IsFromServicesAttribute);
}