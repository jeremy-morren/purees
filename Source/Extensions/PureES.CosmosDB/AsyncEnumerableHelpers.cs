using System.Runtime.CompilerServices;

namespace PureES.CosmosDB;

internal static class AsyncEnumerableHelpers
{
    public static async IAsyncEnumerable<T> Distinct<T>(this IAsyncEnumerable<T> source)
    {
        var list = new HashSet<T>();
        await foreach (var item in source)
            if (list.Add(item))
                yield return item;
    }
    
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this FeedIterator<T> iterator, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        using (iterator)
        {
            while (iterator.HasMoreResults)
                foreach (var item in await iterator.ReadNextAsync(ct))
                    yield return item;
        }
    }
}