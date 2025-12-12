namespace PureES.SourceGenerators;

internal static class TypeNameHelpers
{
    [System.Diagnostics.Contracts.Pure]
    public static string GetGenericTypeName(Type type, params string[] genericArguments)
    {
        var name = type.FullName!;
        var index = name.IndexOf("`", StringComparison.Ordinal);
        return $"global::{name.Substring(0, index)}<{string.Join(", ", genericArguments)}>";
    }

    /// <summary>
    /// Gets a name from a type suitable for use as an identifier
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public static string SanitizeName(IType type)
    {
        var name = type.Name.Replace("[]", "Array");

        name = new[] { '<', '>', '.', '+', ',' }.Aggregate(name, (str, c) => str.Replace(c, '_'));
        
        return name.Replace(" ", string.Empty);
    }
    
    /// <summary>
    /// Transforms a string into a name suitable as a filename
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    [System.Diagnostics.Contracts.Pure]
    public static string SanitizeFilename(string name)
    {
        return Path.GetInvalidFileNameChars().Aggregate(name, (cur, c) => cur.Replace(c, '_'));
    }
}