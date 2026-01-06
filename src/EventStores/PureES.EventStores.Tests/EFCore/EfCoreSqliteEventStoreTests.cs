using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PureES.EventStore.EFCore;
using PureES.EventStore.EFCore.Models;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable UseAwaitUsing

namespace PureES.EventStores.Tests.EFCore;

public class EfCoreSqliteEventStoreTests(ITestOutputHelper output) : EfCoreEventStoreTestsBase
{
    [Fact]
    public async Task EventsShouldSetTimestamp()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<EventStoreDbContext>()
            .UseSqlite(connection)
            .Options;
        
        await using var context = new EventStoreDbContext(options, Options.Create(new EfCoreEventStoreOptions()));
        context.Database.EnsureCreated();

        output.WriteLine(context.Database.GenerateCreateScript());

        var events = Enumerable.Range(0, 10)
            .Select(i => CreateEvent($"test-{i / 2}", i % 2))
            .ToList();
        
        var inserted = await context.Provider.WriteEvents(events, default);
        inserted.Should().HaveCount(10);
        inserted.ShouldAllBe(i => i.Timestamp != default);
        inserted.GroupBy(i => i.Timestamp).Should().HaveCount(1, "All timestamps should be the same");

        var query = context.QueryEvents().ToList();
        query.Should().HaveSameCount(inserted);
        query.Should().AllSatisfy(e => e.EventTypes.ShouldHaveSingleItem().ShouldNotBeNull());
    }

    private static EventStoreEvent CreateEvent(string stream, int position) => new()
    {
        StreamId = stream,
        StreamPos = position,
        TransactionIndex = null,
        EventTypes = EventType.New(["TestEvent" ]),
        Event = JsonSerializer.SerializeToElement(new Dictionary<string, string>()),
        Metadata = JsonSerializer.SerializeToElement(new Dictionary<string, string>()),
    };

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task CreateScriptShouldBeIdempotent()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        var services = new ServiceCollection();

        services.AddDbContext<EmptyDbContext>(b => b.UseSqlite(conn));
        services.AddEfCoreEventStore<EmptyDbContext>();
        
        services.AddPureES().AddBasicEventTypeMap();
        
        var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<IEfCoreEventStore>();
        var script = store.GenerateIdempotentCreateScript();
        Execute(script);
        Execute(script); //Should not throw
        
        //Read events should succeed
        (await store.ReadAll().ToListAsync()).ShouldBeEmpty();

        return;
        
        void Execute(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
    
    protected override Task<EventStoreTestHarness> CreateStore(string testName, Action<IServiceCollection> configureServices, CancellationToken ct)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var services = new ServiceCollection();

        services.AddSingleton(connection); //Dispose will be called by the container

        services.AddDbContext<EmptyDbContext>(b => b.UseSqlite(connection));
        services.AddEfCoreEventStore<EmptyDbContext>().AddSubscriptionToAll();

        services.AddPureES().AddBasicEventTypeMap();
        
        configureServices(services);

        var sp = services.BuildServiceProvider();

        using (var context = sp.GetRequiredService<EventStoreDbContext<EmptyDbContext>>())
            context.Database.EnsureCreated();

        return Task.FromResult(new EventStoreTestHarness(sp, sp.GetRequiredService<IEventStore>()));
    }
}