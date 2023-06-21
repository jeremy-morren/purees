using System.Diagnostics;
using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStores.Tests;

public abstract class EventStoreTestsBase
{
    protected abstract IEventStore CreateStore();

    [DebuggerNonUserCode]
    protected static CancellationToken CancellationToken => new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

    [DebuggerNonUserCode]
    protected static string GetStream(string name) => $"{name}-{Environment.Version}";

    [Fact]
    public async Task Create()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(Create));
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.False(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        Assert.True(await store.Exists(stream, CancellationToken));
        await AssertEqual(events, store.Read(stream, CancellationToken));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }
    
    [Fact]
    public async Task CreateSingle()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(CreateSingle));
        var @event = NewEvent();
        const ulong revision = 0;
        Assert.False(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.Create(stream, @event, CancellationToken));
        Assert.True(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        await AssertEqual(new [] { @event }, store.Read(stream, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
    }

    [Fact]
    public async Task Create_Existing_Should_Throw()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(Create_Existing_Should_Throw));
        const ulong revision = 0;
        Assert.Equal(revision, await store.Create(stream, NewEvent(), CancellationToken));
        var ex = await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.Create(stream, NewEvent(), CancellationToken));
        Assert.Equal(revision, ex.CurrentRevision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append(bool useOptimisticConcurrency)
    {
        var store = CreateStore();
        var stream = GetStream($"{nameof(Append)}+{useOptimisticConcurrency}");
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        Assert.Equal((ulong) 4, await store.Create(stream, events.Take(5), CancellationToken));
        Assert.Equal((ulong) 9, useOptimisticConcurrency
            ? await store.Append(stream, 4, events.Skip(5), CancellationToken)
            : await store.Append(stream, events.Skip(5), CancellationToken));
        await AssertEqual(events, store.Read(stream, CancellationToken));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append_To_Invalid_Should_Throw(bool useOptimisticConcurrency)
    {
        var store = CreateStore();
        var stream = GetStream($"{nameof(Append_To_Invalid_Should_Throw)}+{useOptimisticConcurrency}");
        await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            useOptimisticConcurrency
                ? store.Append(stream, 0, NewEvent(), CancellationToken)
                : store.Append(stream, NewEvent(), CancellationToken));
    }

    [Fact]
    public async Task Append_With_Invalid_Revision_Should_Throw()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(Append_With_Invalid_Revision_Should_Throw));
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.Append(stream, RandVersion(events.Count + 1), NewEvent(), CancellationToken));
        Assert.Equal(revision, ex.ActualRevision);
    }

    [Fact]
    public async Task Read_Invalid_Stream_Should_Throw()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(Read_Invalid_Stream_Should_Throw));
        Assert.False(await store.Exists(stream, CancellationToken));
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, CancellationToken));
        await Assert.ThrowsAsync<StreamNotFoundException>(async () => await store.Read(stream, CancellationToken).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.Read(stream, RandVersion(), CancellationToken).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadPartial(stream, RandVersion(), CancellationToken).FirstAsync());
    }

    [Fact]
    public async Task Read()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(Read));
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));

        await AssertEqual(events, store.Read(stream, revision, CancellationToken));
        await AssertEqual(events.Take(5), store.ReadPartial(stream, 4, CancellationToken));

        async Task AssertWrongVersion(Func<IAsyncEnumerable<EventEnvelope>> getEvents)
        {
            var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () => await getEvents().CountAsync());
            Assert.Equal(revision, ex.ActualRevision);
        }

        await AssertWrongVersion(() => store.Read(stream, RandVersion(events.Count + 1), CancellationToken));

        await AssertWrongVersion(() => store.Read(stream, 0, CancellationToken));

        await AssertWrongVersion(() => store.ReadPartial(stream, RandVersion(events.Count + 1), CancellationToken));
    }

    [Fact]
    public async Task GetStreamRevision()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(GetStreamRevision));
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        await store.Create(stream, events, CancellationToken);
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, revision, CancellationToken));
        await Assert.ThrowsAsync<WrongStreamRevisionException>(() => store.GetRevision(stream, revision + 1, CancellationToken));
    }

    [Fact]
    public async Task Get_Revision_Invalid_Should_Throw()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(Get_Revision_Invalid_Should_Throw));
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, CancellationToken));
    }

    [Fact]
    public async Task ReadAll()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(ReadAll));
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));

        var all = await store.ReadAll(default).ToListAsync();
        Assert.NotEmpty(all);
        
        var sorted = all
            .OrderBy(e => e.Timestamp)
            .ThenBy(a => a.StreamId)
            .ThenBy(a => a.StreamPosition)
            .Select(a => $"{a.StreamId}/{a.StreamPosition}")
            .ToArray();
        
        Assert.Equal(sorted,
            all.Select(a => $"{a.StreamId}/{a.StreamPosition}").ToArray());
        
        Assert.Empty(all.GroupBy(a => new {a.StreamId, a.StreamPosition})
            .Where(g => g.Count() > 1));
    }

    [Fact]
    public async Task StreamPosition_Should_Be_Ordered_And_Unique()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(StreamPosition_Should_Be_Ordered_And_Unique));
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        await store.Create(stream, events.Take(count / 2), CancellationToken);
        await store.Append(stream, events.Skip(count / 2), CancellationToken);

        Assert.Equal((ulong) count - 1, await store.GetRevision(stream, CancellationToken));
        
        var list = await store.Read(stream, CancellationToken).ToListAsync();

        Assert.Equal(list.OrderBy(l => l.StreamPosition)
            .Select(l => l.StreamPosition).ToArray(),
            list.Select(l => l.StreamPosition).ToArray());
        Assert.Equal(Enumerable.Range(0, count), list.Select(e => (int) e.StreamPosition));

        Assert.Equal(list.OrderBy(e => e.Timestamp), list);
    }

    [Fact]
    public async Task ReadByEventType()
    {
        var store = CreateStore();
        
        var stream = GetStream(nameof(ReadByEventType));
        var events = Enumerable.Range(0, 100).Select(_ => NewEvent()).ToList();
        await store.Create(stream, events, CancellationToken);

        Assert.NotEmpty(await store.ReadByEventType(typeof(Event), CancellationToken).ToListAsync());
    }
    
    [Fact]
    public async Task CountByEventType()
    {
        var store = CreateStore();
        
        
        var stream = GetStream(nameof(CountByEventType));
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        await store.Create(stream, events, CancellationToken);

        Assert.Equal((ulong) count, await store.CountByEventType(typeof(Event), CancellationToken));
    }

    [Fact]
    public async Task ReadManyShouldReturnInChronologicalOrder()
    {
        var store = CreateStore();
        
        //We need to add in random order
        var streamIds = Enumerable.Range(0, 10)
            .SelectMany(i => Enumerable.Range(0, 10).Select(_ => GetStream($"stream-{i}")))
            .OrderBy(_ => Guid.NewGuid());

        foreach (var streamId in streamIds)
        {
            try
            {
                await store.Append(streamId, NewEvent(), CancellationToken);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId, NewEvent(), CancellationToken);
            }
            //Append bogus streams to test filter
            try
            {
                await store.Append(streamId + "-other", NewEvent(), CancellationToken);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId + "-other", NewEvent(), CancellationToken);
            }
        }

        var half = Enumerable.Range(0, 10)
            .Select(i => GetStream($"stream-{i}"))
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToHashSet();
        
        var syncResult = await store.ReadMany(half, CancellationToken).ToListAsync();
        var asyncResult = await store.ReadMany(half.ToAsyncEnumerable(), CancellationToken).ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            Assert.NotEmpty(result);
            Assert.Equal(5 * 10, result.Count);
            Assert.Equal(result, result.OrderBy(r => r.Timestamp));

            Assert.Empty(result.Where(e => !half.Contains(e.StreamId)));
        });
    }

    protected static async Task AssertEqual(IEnumerable<UncommittedEvent> source, IAsyncEnumerable<EventEnvelope> events)
    {
        Assert.Equal(source.Select(e => e.EventId),
            await events.Select(e => e.EventId).ToListAsync());
    }

    private static ulong RandVersion(long? min = null) => (ulong) Random.Shared.NextInt64(min ?? 0, int.MaxValue - 1);

    protected static UncommittedEvent NewEvent()
    {
        var id = Guid.NewGuid();
        return new UncommittedEvent(id, new Event(id), new Metadata());
    }

    public record Event(Guid Id);

    protected record Metadata;
}