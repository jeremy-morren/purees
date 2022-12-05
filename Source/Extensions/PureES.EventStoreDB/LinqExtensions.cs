namespace PureES.EventStoreDB;

internal static class LinqExtensions
{
    public static async IAsyncEnumerable<TResult> SelectAwait<TSource, TResult>(this IEnumerable<TSource> source,
        Func<TSource, ValueTask<TResult>> @delegate)
    {
        foreach (var t in source)
            yield return await @delegate(t);
    }
}