using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Npgsql;
using PureES.EventStore.Marten;
using PureES.EventStore.Marten.Subscriptions;
using PureES.EventStores.Tests.Framework.Logging;

namespace PureES.EventStores.Tests;

public class MartenEventStoreTests : EventStoreTestsBase
{
    private readonly ITestOutputHelper _output;

    public MartenEventStoreTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Subscription_To_All_Should_Handle_All_Events()
    {
        var start = DateTime.UtcNow;
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

        await using var harness = await CreateHarness(s =>
        {
            s.AddPureES();
            s.AddSingleton(handler.Object);
        });

        var subscription = harness.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<MartenSubscriptionToAll>().Single();

        await subscription.StartAsync(default); //noop
        
        foreach (var i in Enumerable.Range(0, 10))
            await harness.EventStore.Create(i.ToString(), NewEvent(), default);

        var transaction = new EventsTransaction();
        foreach (var i in Enumerable.Range(100, 10))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, 10).Select(_ => NewEvent()));
        
        await harness.EventStore.SubmitTransaction(transaction.ToUncommittedTransaction(), default);
        
        await subscription.StopAsync(default);

        handler.Verify();
        
        handler.Verify(s => 
                s.Handle(It.Is<EventEnvelope>(e => e.Timestamp != default)),
            Times.Exactly(110));
        
        list.Should().HaveCount(110);

        list.GroupBy(e => e.StreamId).Should().HaveCount(20);
        Assert.All(list.GroupBy(e => e.StreamId), g =>
        {
            var i = int.Parse(g.Key);
            if (i < 100)
                g.ShouldHaveSingleItem();
            else
                g.Should().HaveCount(10);
            
            g.Should().BeInAscendingOrder(e => e.StreamPosition);
            Assert.All(g, e => e.Timestamp.Should().BeOnOrAfter(start).And.BeBefore(DateTime.UtcNow));
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
            .AddMarten(o =>
            {
                o.UseSystemTextJsonForSerialization();
                o.Connection("host=localhost:5432;database=postgres;password=postgres;username=postgres");
                o.DatabaseSchemaName = testName;
            })
            .AddPureESEventStore(o => o.DatabaseSchema = testName)
            .AddPureESSubscriptionToAll()
            .Services
            .AddPureES()
            .AddBasicEventTypeMap()
            .Services;
            
        configureServices(services); 
        var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<IDocumentStore>();

        await using var session = store.LightweightSession();
        await session.ExecuteAsync(new NpgsqlCommand($"drop schema if exists {testName} cascade"), ct);
        var script = store.Storage.ToDatabaseScript();
        //_output.WriteLine(script);
        await session.ExecuteAsync(new NpgsqlCommand(script), ct);

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