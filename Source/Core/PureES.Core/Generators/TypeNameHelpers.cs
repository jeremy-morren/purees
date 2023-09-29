using System.Text;

namespace PureES.Core.Generators;

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
        var sb = new StringBuilder();
        sb.Append(type.Name.Replace("[]", "Array"));
        if (!type.IsGenericType) return sb.ToString();
        foreach (var t in type.GenericArguments)
        {
            sb.Append('_');
            sb.Append(SanitizeName(t));
        }

        return sb.ToString();
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