using JetBrains.Annotations;
using Xunit.Sdk;

// ReSharper disable once CheckNamespace
namespace Xunit;

[PublicAPI]
public static class AssertAsync
{
    public static async Task All<T>(IEnumerable<T> collection, Func<T, Task> @delegate) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(@delegate);

        var results = new Stack<Tuple<int, string, Exception>>();
        var array = collection.ToArray();
        for (var index = 0; index < array.Length; ++index)
            try
            {
                await @delegate(array[index]);
            }
            catch (Exception ex)
            {
                results.Push(new Tuple<int, string, Exception>(index, ArgumentFormatter.Format(array[index]), ex));
            }
        if (results.Count > 0)
            AllException.ForFailures(array.Length, results.ToArray());
    }
    
    public static async Task AllParallel<T>(IEnumerable<T> collection, Func<T, Task> @delegate) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(@delegate);

        var results = new List<Tuple<int, string, Exception>>();

        var array = collection.ToArray();
        
        async Task Run(int index)
        {
            try
            {
                await @delegate(array[index]);
            }
            catch (Exception ex)
            {
                lock (results)
                {
                    results.Add(new Tuple<int, string, Exception>(index, ArgumentFormatter.Format(array[index]), ex));
                }
            }
        }
        
        await Task.WhenAll(Enumerable.Range(0, array.Length).Select(Run));

        if (results.Count > 0)
            AllException.ForFailures(array.Length, 
                results.OrderByDescending(r => r.Item1).ToArray());
    }

    public static async Task All<T>(IAsyncEnumerable<T> collection, Func<T, Task> @delegate) where T : notnull
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (@delegate == null) throw new ArgumentNullException(nameof(@delegate));
        
        var results = new Stack<Tuple<int, string, Exception>>();
        var array = await collection.ToArrayAsync();
        for (var index = 0; index < array.Length; ++index)
            try
            {
                await @delegate(array[index]);
            }
            catch (Exception ex)
            {
                results.Push(new Tuple<int, string, Exception>(index, ArgumentFormatter.Format(array[index]), ex));
            }

        if (results.Count > 0)
            AllException.ForFailures(array.Length, results.ToArray());
    }
    
    public static async Task All<T>(IAsyncEnumerable<T> collection, Action<T> action) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(action);

        Assert.All(await collection.ToListAsync(), action);
    }

    public static async Task NotEmpty<T>(IAsyncEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        await using var enumerator = collection.GetAsyncEnumerator();
        if (!await enumerator.MoveNextAsync())
            NotEmptyException.ForNonEmptyCollection();
    }
}