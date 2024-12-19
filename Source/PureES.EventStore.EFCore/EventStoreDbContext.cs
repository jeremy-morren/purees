using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PureES.EventStore.EFCore;

internal class EventStoreDbContext : DbContext
{
    public EventStoreDbContext(DbContextOptions<EventStoreDbContext> options)
        : base(options) {}


    /// <summary>
    /// Writes the events and returns the events written (with timestamps set)
    /// </summary>
    public async Task<IReadOnlyList<EventStoreEvent>> WriteEvents(IEnumerable<EventStoreEvent> events, CancellationToken ct)
    {
        var list = events.ToList();
        Set<EventStoreEvent>().AddRange(list);
        await SaveChangesAsync(ct);
        return list;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventStoreEvent>(entity =>
        {
            entity.HasKey(e => new { e.StreamId, e.StreamPos });
            entity.Property(e => e.StreamId)
                .IsRequired()
                .ValueGeneratedNever();
            entity.Property(e => e.StreamPos)
                .IsRequired()
                .ValueGeneratedNever();

            if (!IsSqlite())
            {
                // For sqlite, date is set manually
                entity.Property(e => e.Timestamp)
                    .IsRequired()
                    .HasComputedColumnSql(GetServerDateTime(), true);
            }
            
            entity.OwnsOne(e => e.EventTypes).ToJson();
            
            entity.Property(e => e.Data)
                .HasConversion(new JsonElementConverter());
            
            entity.Property(e => e.Metadata)
                .HasConversion(new JsonElementConverter());

            // entity.HasIndex(e => e.EventTypes);
            // entity.HasIndex(e => new { e.Timestamp, e.StreamId, e.StreamPos });
        });
    }

    /// <summary>
    /// Gets a SQL expression for the current server date and time
    /// </summary>
    /// <remarks>
    /// Using the server datetime ensures consistency even for multi-server setups
    /// </remarks>
    private string GetServerDateTime()
    {
        return Database.ProviderName switch
        {
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "transaction_timestamp() at time zone 'utc'",
            "Microsoft.EntityFrameworkCore.SqlServer" => "GETUTCDATE()",
            null => throw new InvalidOperationException("Database provider not set"),
            _ => throw new NotImplementedException($"Database provider {Database.ProviderName} not supported")
        };
    }
    
    private bool IsSqlite() => Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
    
    private class JsonElementConverter : ValueConverter<JsonElement, string>
    {
        public JsonElementConverter(ConverterMappingHints? mappingHints = null) 
            : base(
                e => JsonSerializer.Serialize(e, JsonSerializerOptions.Default),
                s => JsonSerializer.Deserialize<JsonElement>(s, JsonSerializerOptions.Default),
                mappingHints)
        {
        }
    }
}