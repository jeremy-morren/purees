using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.Extensions.Tests.EventStore;

public abstract class EventStoreTestsBase
{
    protected abstract IEventStore CreateStore();

    [Fact]
    public async Task Create()
    {
        var store = CreateStore();
        const string stream = nameof(Create);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, default));
        Assert.Equal(revision, await store.GetRevision(stream, default));
        await AssertEqual(events, store.Read(stream, default));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, default));
    }

    [Fact]
    public async Task Create_Existing_Should_Throw()
    {
        var store = CreateStore();
        const string stream = nameof(Create_Existing_Should_Throw);
        const ulong revision = 0;
        Assert.Equal(revision, await store.Create(stream, NewEvent(), default));
        var ex = await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.Create(stream, NewEvent(), default));
        Assert.Equal(revision, ex.CurrentRevision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append(bool useOptimisticConcurrency)
    {
        var store = CreateStore();
        var stream = nameof(Append)+useOptimisticConcurrency;
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        Assert.Equal((ulong) 4, await store.Create(stream, events.Take(5), default));
        Assert.Equal((ulong) 9, useOptimisticConcurrency
            ? await store.Append(stream, 4, events.Skip(5), default)
            : await store.Append(stream, events.Skip(5), default));
        await AssertEqual(events, store.Read(stream, default));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, default));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append_To_Invalid_Should_Throw(bool useOptimisticConcurrency)
    {
        var store = CreateStore();
        var stream = nameof(Append_To_Invalid_Should_Throw)+useOptimisticConcurrency;
        await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            useOptimisticConcurrency
                ? store.Append(stream, 0, NewEvent(), default)
                : store.Append(stream, NewEvent(), default));
    }

    [Fact]
    public async Task Append_With_Invalid_Revision_Should_Throw()
    {
        var store = CreateStore();
        const string stream = nameof(Append_With_Invalid_Revision_Should_Throw);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, default));
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.Append(stream, RandVersion(events.Count + 1), NewEvent(), default));
        Assert.Equal(revision, ex.ActualRevision);
    }

    [Fact]
    public async Task Read_Invalid_Stream_Should_Throw()
    {
        var store = CreateStore();
        const string stream = nameof(Read_Invalid_Stream_Should_Throw);
        Assert.False(await store.Exists(stream, default));
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, default));
        await Assert.ThrowsAsync<StreamNotFoundException>(async () => await store.Read(stream, default).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.Read(stream, RandVersion(), default).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadPartial(stream, RandVersion(), default).FirstAsync());
    }

    [Fact]
    public async Task Read()
    {
        var store = CreateStore();
        const string stream = nameof(Read);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, default));
        Assert.Equal(revision, await store.GetRevision(stream, default));

        await AssertEqual(events, store.Read(stream, revision, default));
        await AssertEqual(events.Take(5), store.ReadPartial(stream, 4, default));

        async Task AssertWrongVersion(Func<IAsyncEnumerable<EventEnvelope>> getEvents)
        {
            var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () => await getEvents().CountAsync());
            Assert.Equal(revision, ex.ActualRevision);
        }

        await AssertWrongVersion(() => store.Read(stream, RandVersion(events.Count + 1), default));

        await AssertWrongVersion(() => store.Read(stream, 0, default));

        await AssertWrongVersion(() => store.ReadPartial(stream, RandVersion(events.Count + 1), default));
    }

    [Fact]
    public async Task GetStreamRevision()
    {
        var store = CreateStore();
        const string stream = nameof(GetStreamRevision);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        await store.Create(stream, events, default);
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.GetRevision(stream, default));
        Assert.Equal(revision, await store.GetRevision(stream, revision, default));
        await Assert.ThrowsAsync<WrongStreamRevisionException>(() => store.GetRevision(stream, revision + 1, default));
    }

    [Fact]
    public async Task Get_Revision_Invalid_Should_Throw()
    {
        var store = CreateStore();
        const string stream = nameof(Get_Revision_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, default));
    }

    [Fact]
    public async Task ReadAll()
    {
        var store = CreateStore();
        const string stream = nameof(ReadAll);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, default));
        Assert.Equal(revision, await store.GetRevision(stream, default));

        var all = await store.ReadAll(default).ToListAsync();
        Assert.NotEmpty(all);
        Assert.Equal(all.OrderBy(e => e.Timestamp), all);
        Assert.Empty(all.GroupBy(a => new {a.StreamId, a.StreamPosition})
            .Where(g => g.Count() > 1));
    }

    [Fact]
    public async Task StreamPosition_Should_Be_Ordered_And_Unique()
    {
        var store = CreateStore();
        const string stream = nameof(StreamPosition_Should_Be_Ordered_And_Unique);
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        await store.Create(stream, events.Take(count / 2), default);
        await store.Append(stream, events.Skip(count / 2), default);

        Assert.Equal((ulong) count - 1, await store.GetRevision(stream, default));

        
        var list = await store.Read(stream, default).ToListAsync();

        Assert.Equal(list.OrderBy(l => l.StreamPosition), list);
        Assert.Equal(Enumerable.Range(0, count), list.Select(e => (int) e.StreamPosition));

        Assert.Equal(list.OrderBy(e => e.Timestamp), list);
    }

    [Fact]
    public async Task ReadByEventType()
    {
        var store = CreateStore();

        Assert.Empty(await store.ReadByEventType(typeof(int), default).ToListAsync());
    }

    [Fact]
    public async Task ReadManyShouldReturnInChronologicalOrder()
    {
        var store = CreateStore();
        
        //We need to add in random order
        var streamIds = Enumerable.Range(0, 10)
            .SelectMany(i => Enumerable.Range(0, 10).Select(_ => $"stream-{i}"))
            .OrderBy(_ => Guid.NewGuid());

        foreach (var streamId in streamIds)
        {
            try
            {
                await store.Append(streamId, NewEvent(), default);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId, NewEvent(), default);
            }
        }

        var half = Enumerable.Range(0, 10)
            .Select(i => $"stream-{i}")
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToHashSet();
        
        var syncResult = await store.ReadMany(half, default).ToListAsync();
        var asyncResult = await store.ReadMany(half.ToAsyncEnumerable(), default).ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            Assert.NotEmpty(result);
            Assert.Equal(5 * 10, result.Count);
            Assert.Equal(result, result.OrderBy(r => r.Timestamp));

            Assert.Empty(result.Where(e => !half.Contains(e.StreamId)));
        });
    }

    private static async Task AssertEqual(IEnumerable<UncommittedEvent> source, IAsyncEnumerable<EventEnvelope> events)
    {
        Assert.Equal(source.Select(e => new {e.EventId, Event = (Event) e.Event}),
            await events.Select(e => new {e.EventId, Event = (Event) e.Event}).ToListAsync());
    }

    private static ulong RandVersion(long? min = null) => (ulong) Random.Shared.NextInt64(min ?? 0, long.MaxValue);

    protected static UncommittedEvent NewEvent()
    {
        var id = Guid.NewGuid();
        return new UncommittedEvent(id, new Event(id), new Metadata());
    }

    public record Event(Guid Id);

    private record Metadata;
}