using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PureES.EventStore.EFCore.Providers;

namespace PureES.EventStore.EFCore;

internal class EventStoreDbContext : DbContext
{
    private readonly DbContextOptions _options;

    public EventStoreDbContext(DbContextOptions options)
        : base(options)
    {
        _options = options;
    }
    
    #region Provider

    private IEfCoreProvider? _provider;
    public IEfCoreProvider Provider => _provider ??= CreateProvider();

    /// <summary>
    /// Create the provider for the database
    /// </summary>
    private IEfCoreProvider CreateProvider()
    {
        return Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.Sqlite" => new SqliteProvider(this),
            null => throw new InvalidOperationException("Database provider not set"),
            _ => throw new NotImplementedException($"Database provider {Database.ProviderName} not supported")
            
            // "Npgsql.EntityFrameworkCore.PostgreSQL" => "transaction_timestamp() at time zone 'utc'",
            // "Microsoft.EntityFrameworkCore.SqlServer" => "GETUTCDATE()",
        };
    }
    
    #endregion
    
    #region Events
    
    public async Task<List<EventStoreEvent>> WriteAndSaveChanges(List<EventStoreEvent> events, CancellationToken ct)
    {
        await using var context = new EventStoreDbContext(_options);
        context.Set<EventStoreEvent>().AddRange(events);
        await context.SaveChangesAsync(ct);
        return events;
    }

    /// <summary>
    /// Writes the events and returns the events written (with timestamps set)
    /// </summary>
    public Task<List<EventStoreEvent>> WriteEvents(IEnumerable<EventStoreEvent> events, CancellationToken ct)
    {
        return Provider.WriteEvents(events, ct);
    }
    
    public IAsyncEnumerable<EventEnvelope> ReadEvents(IQueryable<EventStoreEvent> query, EfCoreEventSerializer serializer, CancellationToken ct)
    {
        return Provider.ReadEvents(query, serializer, ct);
    }

    public IQueryable<EventStoreEvent> QueryEvents() => Set<EventStoreEvent>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventStoreEvent>(entity =>
        {
            entity.HasKey(e => new { e.StreamId, e.StreamPos });

            entity.Property(e => e.StreamId)
                .IsRequired()
                .ValueGeneratedNever()
                .HasMaxLength(1024);
            
            entity.Property(e => e.StreamPos)
                .IsRequired()
                .ValueGeneratedNever();
            
            entity.OwnsOne(e => e.EventTypes).ToJson();

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(4096);

            Provider.ConfigureEntity(entity);
        });
    }
    
    #endregion
}

/// <summary>
/// A DbContext for the event store that uses options from another context
/// </summary>
internal class EventStoreDbContext<TContext> : EventStoreDbContext
    where TContext : DbContext
{
    public EventStoreDbContext(DbContextOptions<TContext> options)
        : base(CloneOptions(options))
    {
    }
    
    private static DbContextOptions<EventStoreDbContext> CloneOptions(DbContextOptions<TContext> options)
    {
        var builder = new DbContextOptionsBuilder<EventStoreDbContext>();
        foreach (var extension in options.Extensions)
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        return builder.Options;
    }
}