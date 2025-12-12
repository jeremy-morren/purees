using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PureES.EventStores.Tests;

public class AsyncEnumerableGroupTests
{
    [Fact]
    public async Task AsyncGroupSequentialShouldMatch()
    {
        var src = Enumerable.Range(1, 10)
            .SelectMany(i => Enumerable.Range(0, i).Select(_ => i))
            .ToList();
        
        src.AddRange(src); //Duplicate the list

        var grouped = GroupSequential(src, x => x)
            .Select(g => new
            {
                g.Key,
                Values = g.ToList()
            })
            .ToList();
        
        grouped.Should().HaveCount(20);
        Assert.All(grouped, g =>
        {
            var key = g.Values.Distinct().ShouldHaveSingleItem();
            g.Key.ShouldBe(key);
            g.Values.Should().HaveCount(key);
        });

        var asyncGrouped = await src.ToAsyncEnumerable()
            .GroupSequentialBy(x => x)
            .Select(async (g,ct) => new
            {
                g.Key,
                Values = await g.ToListAsync(ct)
            })
            .ToListAsync();
        
        asyncGrouped.Should().HaveCount(20);
        asyncGrouped.Should().BeEquivalentTo(grouped);
    }

    [Fact]
    public async Task RandomSequenceShouldMatch()
    {
        var src = Enumerable.Range(0, 10).ToList();
        
        GroupSequential(src, x => x)
            .Select(g => g.Single())
            .Should().BeEquivalentTo(src);

        var grouped = await src.ToAsyncEnumerable()
            .GroupSequentialBy(x => x)
            .Select(async (g,ct) => await g.SingleAsync(ct))
            .ToListAsync();

        grouped.Should().BeEquivalentTo(src);
    }

    [Fact]
    public async Task PartialReadsOfGroupShouldSucceed()
    {
        var src = Enumerable.Range(2, 10)
            .SelectMany(i => Enumerable.Range(0, i).Select(_ => i))
            .ToList();
        
        src.AddRange(src); //Duplicate the list

        var grouped = GroupSequential(src, x => x)
            .Select(g => new
            {
                g.Key,
                Values = g.Take(g.Key / 2).ToList()
            })
            .ToList();
        
        grouped.Should().HaveCount(20);
        Assert.All(grouped, g =>
        {
            var key = g.Values.Distinct().ShouldHaveSingleItem();
            g.Key.ShouldBe(key);
            g.Values.Should().HaveCount(key / 2);
        });

        var asyncGrouped = await src.ToAsyncEnumerable()
            .GroupSequentialBy(x => x)
            .Select(async (g,ct) => new
            {
                g.Key,
                Values = await g.Take(g.Key / 2).ToListAsync(ct)
            })
            .ToListAsync();
        
        asyncGrouped.Should().HaveCount(20);
        asyncGrouped.Should().BeEquivalentTo(grouped);
    }
    
    [Fact]
    public async Task EmptySequenceShouldReturnEmpty()
    {
        var src = Enumerable.Empty<int>().ToList();
        
        GroupSequential(src, x => x)
            .ShouldBeEmpty();

        var asyncGrouped = await src.ToAsyncEnumerable()
            .GroupSequentialBy(x => x)
            .ToListAsync();
        
        asyncGrouped.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task ConstantSelectorShouldReturnSingleGroup()
    {
        var src = Enumerable.Range(0, 10).ToList();
        
        var asyncGrouped = await src.ToAsyncEnumerable()
            .GroupSequentialBy(_ => "A")
            .ToListAsync();

        asyncGrouped.ShouldHaveSingleItem()
            .Key.ShouldBe("A");
    }

    [Fact]
    public async Task GroupingShouldUseCustomEquality()
    {
        var src = new[] { "A", "a", "b", "B" };
        
        var asyncGrouped = await src.ToAsyncEnumerable()
            .GroupSequentialBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToListAsync();
        
        asyncGrouped.Should().HaveCount(2);
        
        //Check key is the first value encountered
        asyncGrouped[0].Key.ShouldBe("A");
        asyncGrouped[1].Key.ShouldBe("b");
    }
    
    [Fact]
    [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    public async Task EnumeratorShouldRespectCancellation()
    {
        var src = Enumerable.Range(0, 10).ToList();
        
        var cts = new CancellationTokenSource();

        var enumerable = src.ToAsyncEnumerable().GroupSequentialBy(x => x);
        await using var enumerator = enumerable.GetAsyncEnumerator(cts.Token);
        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        var group = enumerator.Current;
        group.Key.ShouldBeEquivalentTo(0);
        
        await using var groupEnumerator = group.GetAsyncEnumerator();
        cts.Cancel();
        (await groupEnumerator.MoveNextAsync()).ShouldBeTrue(); //Value already read
        groupEnumerator.Current.ShouldBe(0);
        
        var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () => await groupEnumerator.MoveNextAsync());
        ex.CancellationToken.ShouldBe(cts.Token);
    }
    
    private static IEnumerable<IGrouping<TKey, TElement>> GroupSequential<TKey, TElement>(
        IEnumerable<TElement> source,
        Func<TElement, TKey> keySelector,
        EqualityComparer<TKey>? comparer = null)
    {
        comparer ??= EqualityComparer<TKey>.Default;
        var group = new List<TElement>();
        foreach (var e in source)
        {
            if (group.Count == 0 || comparer.Equals(keySelector(group[^1]), keySelector(e)))
            {
                group.Add(e);
            }
            else
            {
                yield return new Grouping<TKey, TElement>(keySelector(group[^1]), group);
                group = [e];
            }
        }
        if (group.Count > 0)
            // Return the last group
            yield return new Grouping<TKey, TElement>(keySelector(group[0]), group);
    }

    private class Grouping<TKey, TElement>(TKey key, List<TElement> source) : IGrouping<TKey, TElement>
    {
        public IEnumerator<TElement> GetEnumerator() => source.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => source.GetEnumerator();

        public TKey Key => key;
    }
}