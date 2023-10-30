namespace PureES.Core.SourceGenerators.Generators;

internal static class LinqHelpers
{
    public static int GetIndex<T>(this IEnumerable<T> list, T item) where T : IEquatable<T>
    {
        var i = 0; 
        foreach (var e in list)
        {
            if (e.Equals(item))
                return i;
            i++;
        }
        return -1;
    }
}