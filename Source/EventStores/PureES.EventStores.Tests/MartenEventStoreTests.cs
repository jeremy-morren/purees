using Marten;
using Marten.Services.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Npgsql;
using PureES.Core;
using PureES.EventStores.Marten;
using PureES.EventStores.Marten.Subscriptions;
using PureES.EventStores.Tests.Logging;
using Weasel.Core;

namespace PureES.EventStores.Tests;

public class MartenEventStoreTests : EventStoreTestsBase
{
    private readonly ITestOutputHelper _output;

    public MartenEventStoreTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Subscription_To_All_Should_Handle_All_Events()
    {
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

        var subscription = harness.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<MartenSubscriptionToAll>().Single();

        await subscription.StartAsync(default); //noop

        var transaction = new EventsTransaction();
        foreach (var i in Enumerable.Range(0, 10))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, 10).Select(_ => NewEvent()));
        
        await harness.EventStore.SubmitTransaction(transaction.ToUncommittedTransaction(), default);
        
        await subscription.StopAsync(default);

        handler.Verify(s => 
                s.Handle(It.Is<EventEnvelope>(e => e.Timestamp != default)),
            Times.Exactly(100));

        list.Should().HaveCount(100);

        list.GroupBy(e => e.StreamId).Should().HaveCount(10);
        Assert.All(list.GroupBy(e => e.StreamId), g =>
        {
            g.Should().HaveCount(10);
            g.Should().BeInAscendingOrder(e => e.StreamPosition);
            Assert.All(g, e => e.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1)));
        });
    }
    
    protected override async Task<EventStoreTestHarness> CreateStore(string testName, 
        Action<IServiceCollection> configureServices, 
        CancellationToken ct)
    {
        testName = new[] { '.', '_', '+','-' }.Aggregate(testName, (s, c) => s.Replace(c, '_'));
        testName = $"testing_{testName}";
        var services = new ServiceCollection()
            .AddTestLogging(_output)
            .AddSingleton<IEventTypeMap>(new BasicEventTypeMap())
            .AddMarten(o =>
            {
                o.UseDefaultSerialization(serializerType: SerializerType.SystemTextJson);
                o.Connection(
                    "host=localhost:5432;database=postgres;password=postgres;username=postgres");
                o.DatabaseSchemaName = testName;
            })
            .AddPureESEventStore(o => o.DatabaseSchema = testName)
            .AddPureESSubscriptionToAll()
            .Services;
            
        configureServices(services); 
        var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<IDocumentStore>();
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync(AutoCreate.All);

        var harness = new MartenEventStoreTestHarness(testName, sp);
        return new EventStoreTestHarness(harness, harness.EventStore);
    }
    
    private sealed class MartenEventStoreTestHarness : IAsyncDisposable, IServiceProvider
    {
        private readonly string _testName;
        private readonly ServiceProvider _services;
    
        public MartenEventStoreTestHarness(string testName, ServiceProvider services)
        {
            _testName = testName;
            _services = services;
        }

        public IEventStore EventStore => _services.GetRequiredService<IEventStore>();

        public object? GetService(Type serviceType) => _services.GetService(serviceType);

        public async ValueTask DisposeAsync()
        {
            var store = _services.GetRequiredService<IDocumentStore>();
            await using (var session = store.LightweightSession())
                await session.ExecuteAsync(new NpgsqlCommand($"drop schema if exists {_testName} CASCADE"));
            
            await _services.DisposeAsync();
        }
    }
}