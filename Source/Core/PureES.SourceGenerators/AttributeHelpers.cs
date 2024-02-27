namespace PureES.SourceGenerators;

internal static class AttributeHelpers
{
    public static bool HasFromServicesAttribute(this IParameter parameter) =>
        parameter.Attributes.Any(a => a.Type.Name == "FromServicesAttribute");

    public static bool Contains(this IEnumerable<IAttribute> attributes, string attribute) =>
        attributes.Any(a => a.Type.FullName == attribute);

    public static bool GetEventHandlerPriority(this IEnumerable<IAttribute> attributes, out int priority)
    {
        var attr = attributes.FirstOrDefault(a => a.Type.FullName == "PureES.EventHandlerPriorityAttribute");
        if (attr == null)
        {
            priority = int.MaxValue;
            return false;
        }

        priority = attr.IntParameter;
        return true;
    }
}