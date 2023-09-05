using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.CosmosDB;
using PureES.CosmosDB.Subscription;

namespace PureES.EventStores.Tests.Cosmos;

public class CosmosEventStoreTests : EventStoreTestsBase
{
    protected override async Task<EventStoreTestHarness> CreateStore(string testName, CancellationToken ct)
    {
        var harness = await CosmosTestHarness.Create(testName, ct);
        return new EventStoreTestHarness(harness, harness.GetRequiredService<IEventStore>());
    }


    //TODO: Assert that restartFromBeginning actually does result in replay
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartSubscription(bool restartFromBeginning)
    {
        var name = $"{nameof(StartSubscription)}+{restartFromBeginning}+{Environment.Version}";
        await using var harness = await CosmosTestHarness.Create(name,
            services => services
                .AddCosmosEventStoreSubscriptionToAll(o => o.RestartFromBeginning = restartFromBeginning),
            default);
        
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
        await using var store = await GetStore();
        const string stream = nameof(Bulk_Create);
        var events = Enumerable.Range(0, 10_000)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.False(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        Assert.True(await store.Exists(stream, CancellationToken));
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }
    
    [Fact]
    public async Task Bulk_Create_With_Large_Event()
    {
        await using var store = await GetStore();
        const string stream = nameof(Bulk_Create_With_Large_Event);
        var events = Enumerable.Range(0, 25)
            .Select(_ => NewLargeEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.False(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        Assert.True(await store.Exists(stream, CancellationToken));
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bulk_Append(bool useOptimisticConcurrency)
    {
        await using var store = await GetStore($"{nameof(Bulk_Append)}+{useOptimisticConcurrency}+{Environment.Version}");
        const string stream = nameof(Bulk_Append);
        var events = Enumerable.Range(0, 10_000)
            .Select(_ => NewEvent())
            .ToList();
        const int create = 5;
        Assert.Equal((ulong) create - 1, await store.Create(stream, events.Take(create), CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, useOptimisticConcurrency
            ? await store.Append(stream, create - 1, events.Skip(create), CancellationToken)
            : await store.Append(stream, events.Skip(create), CancellationToken));
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bulk_Append_With_Large_Event(bool useOptimisticConcurrency)
    {
        await using var store = await GetStore($"{nameof(Bulk_Append_With_Large_Event)}+{useOptimisticConcurrency}+{Environment.Version}");
        const string stream = nameof(Bulk_Append_With_Large_Event);
        var events = Enumerable.Range(0, 25)
            .Select(_ => NewLargeEvent())
            .ToList();
        const int create = 5;
        Assert.Equal((ulong) create - 1, await store.Create(stream, events.Take(create), CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, useOptimisticConcurrency
            ? await store.Append(stream, create - 1, events.Skip(create), CancellationToken)
            : await store.Append(stream, events.Skip(create), CancellationToken));
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }

    private static UncommittedEvent NewLargeEvent()
    {
        var data = new byte[1024 * 512]; //.5 MB
        return new UncommittedEvent() { Event = new LargeEvent(data) };
    }

    private record LargeEvent(byte[] Data);
}