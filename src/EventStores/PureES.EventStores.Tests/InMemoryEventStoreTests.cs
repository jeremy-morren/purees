using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PureES.EventStore.InMemory;
using PureES.EventStore.InMemory.Subscription;

namespace PureES.EventStores.Tests;

public class InMemoryEventStoreTests : EventStoreTestsBase
{
    [Fact]
    public async Task Subscription_To_All_Should_Handle_All_Events()
    {
        const string streamId = nameof(Subscription_To_All_Should_Handle_All_Events);

        var handler = new Mock<IEventHandler>();

        var list = new List<EventEnvelope>();
        
        handler.Setup(s => s.Handle(It.IsAny<EventEnvelope>()))
            .Callback((EventEnvelope e) =>
            {
                lock (list)
                {
                    list.Add(e);
                }
            });
        
        await using var harness = await CreateHarness(s => s.AddSingleton(handler.Object));
        var store = harness.EventStore;

        var subscription = harness.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<InMemoryEventStoreSubscriptionToAll>().Single();
        
        await subscription.StartAsync(default); //noop
        
        (await store.Create(streamId, Enumerable.Range(0, 10).Select(_ => NewEvent()), default)).ShouldBe(9u);

        var transaction = new EventsTransaction();
        foreach (var i in Enumerable.Range(0, 10))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, 10).Select(_ => NewEvent()));

        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), default);
        
        await subscription.StopAsync(default);

        handler.Verify();
        
        handler.Verify(s => s.Handle(It.Is<EventEnvelope>(e => e.StreamId == streamId)),
            Times.Exactly(10));
        
        handler.Verify(s => s.Handle(It.IsAny<EventEnvelope>()), Times.Exactly(110));
        
        list.Should().HaveCount(110);
        list.GroupBy(e => e.StreamId).Should().HaveCount(11);
        
        Assert.All(list.GroupBy(e => e.StreamId), g =>
        {
            g.Should().HaveCount(10);
            g.Should().BeInAscendingOrder(l => l.StreamPosition);
            g.ShouldAllBe(e => e.Timestamp == g.First().Timestamp);
        });
    }

    [Fact]
    public async Task HandleLoad()
    {
        const string streamId = nameof(HandleLoad);

        await using var harness = await CreateHarness();

        for (var i = 0; i < 10; i++)
        {
            (await harness.EventStore.Create($"{streamId}-{i}", 
                Enumerable.Range(0, 10).Select(_ => NewEvent()), 
                default)).ShouldBe(9u);
        }

        var store = new ServiceCollection()
            .AddInMemoryEventStore()
            .AddSubscriptionToAll()
            .Services
            .AddSingleton<IEventTypeMap, BasicEventTypeMap>()
            .BuildServiceProvider()
            .GetRequiredService<IInMemoryEventStore>();
        
        await store.Load(harness.EventStore.ReadAll(Direction.Forwards), default);

        store.ReadAllSync().Should().HaveCount(100);
        store.Serialize().Should().HaveCount(100);
        Assert.All(Enumerable.Range(0, 10), i =>
        {
            var sId = $"{streamId}-{i}";
            store.ExistsSync(sId).ShouldBeTrue();
            
            var events = store.ReadSync(sId).ToList();
            events.Should().HaveCount(10);
            events.ShouldAllBe(e => e.StreamId == sId);
            events.Should().BeInAscendingOrder(e => e.StreamPosition);
        });
    }

    [Fact]
    public async Task Serialize()
    {
        const string streamId = nameof(Serialize);

        await using var harness = await CreateHarness();
        
        var store = ((IInMemoryEventStore)harness.EventStore);

        for (var i = 0; i < 10; i++)
        {
            (await store.Create($"{streamId}-{i}", 
                Enumerable.Range(0, 10).Select(_ => NewEvent()), 
                default)).ShouldBe(9u);
        }

        var serialized = store.Serialize().ToList();
        var jsonTypeInfo = InMemoryEventStoreJsonSerializerContext.Default.ListSerializedInMemoryEventRecord;
        serialized = JsonSerializer.SerializeToElement(serialized, jsonTypeInfo)
            .Deserialize<List<SerializedInMemoryEventRecord>>(jsonTypeInfo)
            .ShouldNotBeNull();
        
        store = new ServiceCollection()
            .AddInMemoryEventStore()
            .AddSubscriptionToAll()
            .Services
            .AddSingleton<IEventTypeMap, BasicEventTypeMap>()
            .BuildServiceProvider()
            .GetRequiredService<IInMemoryEventStore>();

        store.Load(serialized);

        store.ReadAllSync().Should().HaveCount(100);
        await Assert.AllAsync(Enumerable.Range(0, 10), async i =>
        {
            var sId = $"{streamId}-{i}";
            store.ExistsSync(sId).ShouldBeTrue();
            (await store.Exists(sId, default)).ShouldBeTrue();

            var events = await store.Read(sId).ToListAsync();
            store.ReadSync(sId).Should().HaveCount(10);
            events.Should().HaveCount(10);
            events.ShouldAllBe(e => e.StreamId == sId);
            events.Should().BeInAscendingOrder(e => e.StreamPosition);
        });
    }
    
    protected override Task<EventStoreTestHarness> CreateStore(string testName, 
        Action<IServiceCollection> configureServices, 
        CancellationToken ct)
    {
        var services = new ServiceCollection()
            .AddPureES().Services
            .AddInMemoryEventStore()
            .AddSubscriptionToAll()
            .Services
            .AddSingleton<IEventTypeMap, BasicEventTypeMap>();

        configureServices(services);

        var sp = services.BuildServiceProvider();
        
        return Task.FromResult(new EventStoreTestHarness(sp, sp.GetRequiredService<IEventStore>()));
    }
}