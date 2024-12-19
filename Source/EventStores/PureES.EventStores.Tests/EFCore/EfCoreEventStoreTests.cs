using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PureES.EventStore.EFCore;
// ReSharper disable MethodHasAsyncOverload
// ReSharper disable UseAwaitUsing

namespace PureES.EventStores.Tests.EFCore;

public class EfCoreEventStoreTests
{
    [Fact]
    public async Task SqliteEventsShouldSetTimestamp()
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
        inserted.ShouldAllBe(i => i.Timestamp.Kind == DateTimeKind.Utc);
        inserted.ShouldAllBe(i => i.Timestamp != default);
        inserted.GroupBy(i => i.Timestamp).Should().HaveCount(1, "All timestamps should be the same");
    }

    private static EventStoreEvent CreateEvent(string stream, int position) => new()
    {
        StreamId = stream,
        StreamPos = (uint)position,
        EventTypes = ["TestEvent"],
        Data = JsonSerializer.SerializeToElement(new Dictionary<string, string>()),
        Metadata = JsonSerializer.SerializeToElement(new Dictionary<string, string>()),
    };
}