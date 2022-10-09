using System.Text.Json.Nodes;
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
        await AssertEqual(events, store.Load(stream, default));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, default));
    }
    
    [Fact]
    public async Task Create_Existing_Should_Throw()
    {
        var store = CreateStore();
        const string stream = nameof(Create_Existing_Should_Throw);
        const ulong revision = 0;
        Assert.Equal(revision, await store.Create(stream, NewEvent(), default));
        var ex = await Assert.ThrowsAsync<StreamAlreadyExistsException>(() => store.Create(stream, NewEvent(), default));
        Assert.Equal(revision, ex.CurrentRevision);
    }

    [Fact]
    public async Task Append()
    {
        var store = CreateStore();
        const string stream = nameof(Append);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        Assert.Equal((ulong)4, await store.Create(stream, events.Take(5), default));
        Assert.Equal((ulong) 9, await store.Append(stream, 4, events.Skip(5), default));
        await AssertEqual(events, store.Load(stream, default));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, default));
    }
    
    [Fact]
    public async Task Append_To_Invalid_Should_Throw()
    {
        var store = CreateStore();
        const string stream = nameof(Append_To_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.Append(stream, 0, NewEvent(), default));
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
    public async Task Load_Invalid_Stream_Should_Throw()
    {
        var store = CreateStore();
        const string stream = nameof(Load_Invalid_Stream_Should_Throw);
        Assert.False(await store.Exists(stream, default));
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, default));
        await Assert.ThrowsAsync<StreamNotFoundException>(async () => await store.Load(stream, default).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.Load(stream, RandVersion(), default).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.LoadPartial(stream, RandVersion(), default).FirstAsync());
    }
    
    [Fact]
    public async Task Load()
    {
        var store = CreateStore();
        const string stream = nameof(Load);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, default));
        Assert.Equal(revision, await store.GetRevision(stream, default));
        
        await AssertEqual(events, store.Load(stream, revision, default));
        await AssertEqual(events.Take(5), store.LoadPartial(stream, 4, default));

        async Task AssertWrongVersion(Func<IAsyncEnumerable<EventEnvelope>> getEvents)
        {
            var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () => await getEvents().CountAsync());
            Assert.Equal(revision, ex.ActualRevision);
        }
        await AssertWrongVersion(() => store.Load(stream, RandVersion(events.Count + 1), default));
        
        await AssertWrongVersion(() => store.Load(stream, 0, default));

        await AssertWrongVersion(() => store.LoadPartial(stream, RandVersion(events.Count + 1), default));
    }

    [Fact]
    public async Task LoadByEventType()
    {
        var store = CreateStore();
        
        Assert.Empty(await store.LoadByEventType(typeof(int), default).ToListAsync());
    }

    private static async Task AssertEqual(IEnumerable<UncommittedEvent> source, IAsyncEnumerable<EventEnvelope> @events)
    {
        Assert.Equal(source.Select(e => e.EventId), 
            await @events.Select(e => e.EventId).ToListAsync());
    }

    private static ulong RandVersion(long? min = null) => (ulong)Random.Shared.NextInt64(min ?? 0, long.MaxValue);

    private static UncommittedEvent NewEvent()
    {
        var id = Guid.NewGuid();
        return new UncommittedEvent(id, JsonNode.Parse($"{{ \"id\": \"{id}\" }}")!, null);
    }
}