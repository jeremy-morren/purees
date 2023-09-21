namespace PureES.Core.Generators;

public static class TypeNameHelpers
{
    [System.Diagnostics.Contracts.Pure]
    public static string GetGenericTypeName(Type type, params string[] genericArguments)
    {
        var name = type.FullName!;
        var index = name.IndexOf("`", StringComparison.Ordinal);
        return $"global::{name.Substring(0, index)}<{string.Join(", ", genericArguments)}>";
    }

    /// <summary>
    /// Transforms a string into a name suitable as an object name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    [System.Diagnostics.Contracts.Pure]
    public static string SanitizeName(string name)
    {
        name = name.Replace("global::", string.Empty);
        name = name.Replace("[]", "Array");
        return new[] { '<', '>', '[', ']', '+', '.' }.Aggregate(name, (cur, c) => cur.Replace(c, '_'));
    }
}