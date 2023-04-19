using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PureES.Core.EventStore;
using PureES.CosmosDB;
using PureES.CosmosDB.Subscription;
using PureES.EventBus;

namespace PureES.Extensions.Tests.EventStore.Cosmos;

public class CosmosEventStoreTests : EventStoreTestsBase, IClassFixture<CosmosTestFixture>
{
    private readonly CosmosTestFixture _fixture;

    public CosmosEventStoreTests(CosmosTestFixture fixture) => _fixture = fixture;
    protected override IEventStore CreateStore() => _fixture.GetRequiredService<IEventStore>();

    //TODO: Assert that restartFromBeginning actually does result in replay
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartSubscription(bool restartFromBeginning)
    {
        await using var harness = new CosmosTestFixture(services =>
            services.AddCosmosEventStoreSubscriptionToAll(o => o.RestartFromBeginning = restartFromBeginning)
                .AddEventBus());
        
        var subscription = (CosmosEventStoreSubscriptionToAll)harness.GetServices<IHostedService>()
            .Single(s => s.GetType() == typeof(CosmosEventStoreSubscriptionToAll));

        var processor = await subscription.CreateProcessor(default);

        await processor.StartAsync();

        await processor.StopAsync();
    }
    
    //CosmosDB has a limit of 2MB transactional batch size
    //This ensures that we succeed if we try to insert a large number of events

    [Fact]
    public async Task Bulk_Create()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(Bulk_Create));
        var events = Enumerable.Range(0, 10_000)
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
    public async Task Bulk_Create_With_Large_Event()
    {
        var store = CreateStore();
        var stream = GetStream(nameof(Bulk_Create_With_Large_Event));
        var events = Enumerable.Range(0, 25)
            .Select(_ => NewLargeEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.False(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        Assert.True(await store.Exists(stream, CancellationToken));
        await AssertEqual(events, store.Read(stream, CancellationToken));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bulk_Append(bool useOptimisticConcurrency)
    {
        var store = CreateStore();
        var stream = GetStream($"{nameof(Bulk_Append)}+{useOptimisticConcurrency}");
        var events = Enumerable.Range(0, 10_000)
            .Select(_ => NewEvent())
            .ToList();
        const int create = 5;
        Assert.Equal((ulong) create - 1, await store.Create(stream, events.Take(create), CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, useOptimisticConcurrency
            ? await store.Append(stream, create - 1, events.Skip(create), CancellationToken)
            : await store.Append(stream, events.Skip(create), CancellationToken));
        await AssertEqual(events, store.Read(stream, CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bulk_Append_With_Large_Event(bool useOptimisticConcurrency)
    {
        var store = CreateStore();
        var stream = GetStream($"{nameof(Bulk_Append_With_Large_Event)}+{useOptimisticConcurrency}");
        var events = Enumerable.Range(0, 25)
            .Select(_ => NewLargeEvent())
            .ToList();
        const int create = 5;
        Assert.Equal((ulong) create - 1, await store.Create(stream, events.Take(create), CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, useOptimisticConcurrency
            ? await store.Append(stream, create - 1, events.Skip(create), CancellationToken)
            : await store.Append(stream, events.Skip(create), CancellationToken));
        await AssertEqual(events, store.Read(stream, CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }

    private static UncommittedEvent NewLargeEvent()
    {
        var data = new byte[1024 * 512]; //.5 MB
        return new UncommittedEvent(Guid.NewGuid(), new LargeEvent(data), new Metadata());
    }

    private record LargeEvent(byte[] Data);
}