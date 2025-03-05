using System.Text;

namespace PureES.EventBus;

/// <summary>
/// Display type names
/// </summary>
internal static class TypeNameFormatter
{
    /// <summary>
    /// Gets a type name suitable for display (including generic parameters, without namespace or assembly)
    /// </summary>
    public static string GetDisplayTypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var name = GetNestedName(type);
        if (!type.IsGenericType) return name;
        //Recursively write generic parameters
        var args = type.GetGenericArguments().Select(GetDisplayTypeName);
        return $"{name}<{string.Join(", ", args)}>";
    }

    private static string GetNestedName(Type type) => type.IsNested ? $"{GetNestedName(type.DeclaringType!)}+{type.Name}" : type.Name;
}