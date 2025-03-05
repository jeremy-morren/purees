using System.Text.RegularExpressions;

namespace PureES.EventStore.EFCore;

internal static partial class SqlRegexes
{
    /// <summary>
    /// Replaces CREATE (TABLE|INDEX) with CREATE IF NOT EXISTS (TABLE|INDEX)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static string ReplaceCreateWithCreateIfNotExists(string source)
    {
        source = ReplaceRegex().Replace(source, "$1 IF NOT EXISTS");
        return source;
    }
    
    [GeneratedRegex("(?<=CREATE\\W+)(TABLE|INDEX)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReplaceRegex();
}