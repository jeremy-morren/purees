namespace PureES.SourceGenerators;

internal static class AttributeHelpers
{
    public static bool HasFromServicesAttribute(this IParameter parameter) =>
        parameter.Attributes.Any(a => a.Type.Name == "FromServicesAttribute");

    public static bool Contains(this IEnumerable<IAttribute> attributes, string attribute) =>
        attributes.Any(a => a.Type.FullName == attribute);
}