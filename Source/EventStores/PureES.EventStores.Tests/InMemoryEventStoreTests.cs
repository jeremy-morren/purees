using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using PureES.Core;
using PureES.EventStore.InMemory;
using PureES.EventStore.InMemory.Subscription;
using Shouldly;

namespace PureES.EventStores.Tests;

public class InMemoryEventStoreTests : EventStoreTestsBase
{
    [Fact]
    public async Task Subscription_To_All_Should_Handle_All_Events()
    {
        const string streamId = nameof(Subscription_To_All_Should_Handle_All_Events);

        var handler = new Mock<IEventHandler>();

        var list = new List<EventEnvelope>();

        handler.Setup(s => s.Handle(It.Is<EventEnvelope>(e => e.StreamId == streamId)))
            .Callback((EventEnvelope e) =>
            {
                lock (list)
                {
                    list.Add(e);
                }
            });

        await using var harness = await GetStore(s => s.AddSingleton(handler.Object));

        var subscription = harness.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<InMemoryEventStoreSubscriptionToAll>().Single();
        
        await subscription.StartAsync(default); //noop
        
        (await harness.Create(streamId, Enumerable.Range(0, 10).Select(_ => NewEvent()), default)).ShouldBe(9ul);
        
        await subscription.StopAsync(default);

        handler.Verify(s => s.Handle(It.Is<EventEnvelope>(e => e.StreamId == streamId)),
            Times.Exactly(10));
        
        list.Should().HaveCount(10);
        list.Should().BeInAscendingOrder(l => l.StreamPosition);
        Assert.All(list, e =>
        {
            e.StreamId.ShouldBe(streamId);
            e.Timestamp.ShouldBe(list[0].Timestamp);
        });
    }
    
    protected override Task<EventStoreTestHarness> CreateStore(string testName, 
        Action<IServiceCollection> configureServices, 
        CancellationToken ct)
    {
        var services = new ServiceCollection()
            .AddInMemoryEventStore()
            .AddInMemorySubscriptionToAll()
            .AddSingleton<IEventTypeMap, BasicEventTypeMap>();

        configureServices(services);

        var sp = services.BuildServiceProvider();
        
        return Task.FromResult(new EventStoreTestHarness(sp, sp.GetRequiredService<IEventStore>()));
    }

    //TODO: test subscriptions
    
    private static InMemoryEventStoreSerializer Serializer =>
        new (new BasicEventTypeMap(), new OptionsWrapper<InMemoryEventStoreOptions>(new InMemoryEventStoreOptions()));
}