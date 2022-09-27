using System.Text.Json;
using System.Text.Json.Nodes;
using EventStore.Client;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.EventStore.Tests.Framework;
using PureES.EventStoreDB;
using StreamNotFoundException = PureES.Core.EventStore.StreamNotFoundException;

namespace PureES.EventStore.Tests;

public class EventStoreClientTests : IClassFixture<EventStoreTestHarness>
{
    private readonly EventStoreTestHarness _eventStore;

    public EventStoreClientTests(EventStoreTestHarness eventStore) => _eventStore = eventStore;

    [Fact]
    public async Task Create()
    {
        var client = new EventStoreDBClient(_eventStore.GetClient(), new TestSerializer());
        const string stream = nameof(Create);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await client.Create(stream, events, default));
        Assert.Equal(revision, await client.GetRevision(stream, default));
        await AssertEqual(events, client.Load(stream, default));
        Assert.Equal((ulong) events.Count - 1, await client.GetRevision(stream, default));
    }
    
    [Fact]
    public async Task Create_Existing_Should_Throw()
    {
        var client = new EventStoreDBClient(_eventStore.GetClient(), new TestSerializer());
        const string stream = nameof(Create_Existing_Should_Throw);
        Assert.Equal((ulong)0, await client.Create(stream, NewEvent(), default));
        await Assert.ThrowsAsync<StreamAlreadyExistsException>(() => client.Create(stream, NewEvent(), default));
    }

    [Fact]
    public async Task Append()
    {
        var client = new EventStoreDBClient(_eventStore.GetClient(), new TestSerializer());
        const string stream = nameof(Append);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        Assert.Equal((ulong)4, await client.Create(stream, events.Take(5), default));
        Assert.Equal((ulong) 9, await client.Append(stream, 4, events.Skip(5), default));
        await AssertEqual(events, client.Load(stream, default));
        Assert.Equal((ulong) events.Count - 1, await client.GetRevision(stream, default));
    }
    
    [Fact]
    public async Task Append_To_Invalid_Should_Throw()
    {
        var client = new EventStoreDBClient(_eventStore.GetClient(), new TestSerializer());
        const string stream = nameof(Append_To_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() => client.Append(stream, 0, NewEvent(), default));
    }
    
    [Fact]
    public async Task Append_With_Invalid_Revision_Should_Throw()
    {
        var client = new EventStoreDBClient(_eventStore.GetClient(), new TestSerializer());
        const string stream = nameof(Append_With_Invalid_Revision_Should_Throw);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        await client.Create(stream, events, default);
        await Assert.ThrowsAsync<WrongStreamVersionException>(() =>
            client.Append(stream, RandVersion(events.Count + 1), NewEvent(), default));
    }
    
    [Fact]
    public async Task Load_Invalid_Stream_Should_Throw()
    {
        var client = new EventStoreDBClient(_eventStore.GetClient(), new TestSerializer());
        const string stream = nameof(Load_Invalid_Stream_Should_Throw);
        Assert.False(await client.Exists(stream, default));
        await Assert.ThrowsAsync<StreamNotFoundException>(() => client.GetRevision(stream, default));
        await Assert.ThrowsAsync<StreamNotFoundException>(async () => await client.Load(stream, default).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await client.Load(stream, RandVersion(), default).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await client.LoadPartial(stream, RandVersion(), default).FirstAsync());
    }
    
    [Fact]
    public async Task Load()
    {
        var client = new EventStoreDBClient(_eventStore.GetClient(), new TestSerializer());
        const string stream = nameof(Load);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await client.Create(stream, events, default));
        Assert.Equal(revision, await client.GetRevision(stream, default));
        await AssertEqual(events, client.Load(stream, revision, default));

        await Assert.ThrowsAsync<WrongStreamVersionException>(async () =>
            await client.Load(stream, RandVersion(events.Count + 1), default).CountAsync());
        
        await Assert.ThrowsAsync<WrongStreamVersionException>(async () =>
            await client.Load(stream, 0, default).CountAsync());

        await AssertEqual(events.Take(5), client.LoadPartial(stream, 4, default));

        await Assert.ThrowsAsync<WrongStreamVersionException>(async () =>
            await client.LoadPartial(stream, RandVersion(events.Count + 1), default).CountAsync());
    }

    [Fact]
    public async Task LoadByEventType()
    {
        var client = new EventStoreDBClient(_eventStore.GetClient(), new TestSerializer());
        
        Assert.Empty(await client.LoadByEventType(typeof(int), default).ToListAsync());
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