using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PureES.EventStore.CosmosDB;
using PureES.EventStore.CosmosDB.Subscriptions;

namespace PureES.EventStores.Tests.Cosmos;

[Trait("Requires", "CosmosDB")]
public class CosmosEventStoreTests : EventStoreTestsBase
{
    protected override async Task<EventStoreTestHarness> CreateStore(string testName,
        Action<IServiceCollection> configureServices,
        CancellationToken ct)
    {
        var harness = await CosmosTestHarness.Create(testName, configureServices);

        await CosmosEventStoreSetup.InitializeEventStore(harness, ct);
        
        return new EventStoreTestHarness(harness, harness.GetRequiredService<IEventStore>());
    }

    [Fact]
    public async Task StartSubscription()
    {
        var name = $"{nameof(StartSubscription)}+{Environment.Version}";

        await using var harness = await CosmosTestHarness.Create(name,
            services => services.AddCosmosEventStoreSubscriptionToAll());
        
        await CosmosEventStoreSetup.InitializeEventStore(harness, CancellationToken);
        
        var subscription = (CosmosEventStoreSubscriptionToAll)harness.GetServices<IHostedService>()
            .Single(s => s.GetType() == typeof(CosmosEventStoreSubscriptionToAll));

        var processor = subscription.CreateProcessor();
        
        await processor.StartAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));

        await processor.StopAsync();
    }
    
    //CosmosDB has a limit of 2MB transactional batch size
    //This ensures that we succeed if we try to insert a large number of events

    [Fact]
    public async Task Bulk_Create()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Bulk_Create);
        var events = Enumerable.Range(0, 10_000)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (uint) events.Count - 1;
        (await store.Exists(stream, CancellationToken)).ShouldBeFalse();
        (await store.Create(stream, events, CancellationToken)).ShouldBe(revision);

        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);

        (await store.Exists(stream, CancellationToken)).ShouldBeTrue();
        
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        (await store.GetRevision(stream, CancellationToken)).ShouldBe((uint) events.Count - 1);
    }
    
    [Fact]
    public async Task Bulk_Create_With_Large_Event()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Bulk_Create_With_Large_Event);
        var events = Enumerable.Range(0, 25)
            .Select(_ => NewLargeEvent())
            .ToList();
        var revision = (uint) events.Count - 1;
        (await store.Exists(stream, CancellationToken)).ShouldBeFalse();
        (await store.Create(stream, events, CancellationToken)).ShouldBe(revision);

        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);

        (await store.Exists(stream, CancellationToken)).ShouldBeTrue();
        
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        (await store.GetRevision(stream, CancellationToken)).ShouldBe((uint) events.Count - 1);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bulk_Append(bool useOptimisticConcurrency)
    {
        await using var harness = await CreateHarness($"{nameof(Bulk_Append)}+{useOptimisticConcurrency}+{Environment.Version}");
        var store = harness.EventStore;
        const string stream = nameof(Bulk_Append);
        var events = Enumerable.Range(0, 10_000)
            .Select(_ => NewEvent())
            .ToList();
        const int create = 5;
        Assert.Equal((uint) create - 1, await store.Create(stream, events.Take(create), CancellationToken));
        
        Assert.Equal((uint) events.Count - 1, useOptimisticConcurrency
            ? await store.Append(stream, create - 1, events.Skip(create), CancellationToken)
            : await store.Append(stream, events.Skip(create), CancellationToken));
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        
        Assert.Equal((uint) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bulk_Append_With_Large_Event(bool useOptimisticConcurrency)
    {
        await using var harness = await CreateHarness($"{nameof(Bulk_Append_With_Large_Event)}+{useOptimisticConcurrency}+{Environment.Version}");
        var store = harness.EventStore;
        const string stream = nameof(Bulk_Append_With_Large_Event);
        var events = Enumerable.Range(0, 25)
            .Select(_ => NewLargeEvent())
            .ToList();
        const int create = 5;
        Assert.Equal((uint) create - 1, await store.Create(stream, events.Take(create), CancellationToken));
        
        Assert.Equal((uint) events.Count - 1, useOptimisticConcurrency
            ? await store.Append(stream, create - 1, events.Skip(create), CancellationToken)
            : await store.Append(stream, events.Skip(create), CancellationToken));
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        
        Assert.Equal((uint) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }

    private static UncommittedEvent NewLargeEvent()
    {
        var data = new byte[1024 * 512]; //512 KB
        return new UncommittedEvent(new LargeEvent(Guid.NewGuid(), data));
    }

    [UsedImplicitly]
    private record LargeEvent(Guid Id, byte[] Data) : Event(Id);
}