using System.Diagnostics.Contracts;

namespace PureES.Core.Generators;

public static class TypeNameHelpers
{
    [Pure]
    public static string GetGenericTypeName(Type type, string genericArgument)
    {
        var name = type.FullName!;
        var index = name.IndexOf("`", StringComparison.Ordinal);
        return $"global::{name.Substring(0, index)}<{genericArgument}>";
    }

    /// <summary>
    /// Transforms a string into a name suitable as an object name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    [Pure]
    public static string SanitizeName(string name)
    {
        name = name.Replace("global::", string.Empty);
        name = name.Replace("[]", "Array");
        return new[] { '<', '>', '[', ']', '+', '.' }.Aggregate(name, (cur, c) => cur.Replace(c, '_'));
    }
}