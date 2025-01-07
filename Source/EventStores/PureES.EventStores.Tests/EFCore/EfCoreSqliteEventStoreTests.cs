using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PureES.EventStore.EFCore;
// ReSharper disable MethodHasAsyncOverload
// ReSharper disable UseAwaitUsing

namespace PureES.EventStores.Tests.EFCore;

public class EfCoreSqliteEventStoreTests : EventStoreTestsBase
{
    [Fact]
    public async Task EventsShouldSetTimestamp()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<EventStoreDbContext>()
            .UseSqlite(connection)
            .Options;
        
        await using var context = new EventStoreDbContext(options);
        context.Database.EnsureCreated();

        var events = Enumerable.Range(0, 10)
            .Select(i => CreateEvent($"test-{i / 2}", i % 2))
            .ToList();

        var inserted = await context.WriteEvents(events, default);
        inserted.Should().HaveCount(10);
        inserted.ShouldAllBe(i => i.Timestamp != default);
        inserted.GroupBy(i => i.Timestamp).Should().HaveCount(1, "All timestamps should be the same");

        var query = context.QueryEvents().ToList();
        query.Should().HaveSameCount(inserted);
        query.ShouldAllBe(q => q.EventTypes.Length == 1);
    }

    [Fact]
    public async Task EventsTypesShouldBeWritten()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<EventStoreDbContext>()
            .UseSqlite(connection)
            .Options;
        
        await using var context = new EventStoreDbContext(options);
        context.Database.EnsureCreated();

        var events = Enumerable.Range(0, 10)
            .Select(i => CreateEvent($"test-{i / 2}", i % 2))
            .ToList();

        var inserted = await context.WriteEvents(events, default);
        inserted.Should().HaveCount(10);
        inserted.ShouldAllBe(i => i.EventTypes.Length == 1);
        inserted.ShouldAllBe(i => i.Timestamp != default);
        inserted.GroupBy(i => i.Timestamp).Should().HaveCount(1, "All timestamps should be the same");

        var types = context.Database.SqlQueryRaw<string>("select EventTypes from EventStoreEvent").ToList();
        types.Should().AllSatisfy(t => JsonSerializer.Deserialize<string[]>(t).ShouldHaveSingleItem());
    }

    private static EventStoreEvent CreateEvent(string stream, int position) => new()
    {
        StreamId = stream,
        StreamPos = position,
        EventTypes = ["TestEvent"],
        Data = JsonSerializer.SerializeToElement(new Dictionary<string, string>()),
        Metadata = JsonSerializer.SerializeToElement(new Dictionary<string, string>()),
    };

    protected override Task<EventStoreTestHarness> CreateStore(string testName, Action<IServiceCollection> configureServices, CancellationToken ct)
    {
        var services = new ServiceCollection();
        
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        
        services.AddSingleton(connection); //Dispose will be called by the container

        services.AddDbContext<NoOpDbContext>(builder => builder.UseSqlite(connection));

        services.AddEfCoreEventStore<NoOpDbContext>();

        services.AddPureES().AddBasicEventTypeMap();
        
        configureServices(services);

        var sp = services.BuildServiceProvider();

        using (var context = sp.GetRequiredService<EventStoreDbContext<NoOpDbContext>>())
            context.Database.EnsureCreated();

        return Task.FromResult(new EventStoreTestHarness(sp, sp.GetRequiredService<IEventStore>()));
    }
}