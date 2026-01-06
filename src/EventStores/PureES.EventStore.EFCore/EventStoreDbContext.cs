using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using PureES.EventStore.EFCore.Models;
using PureES.EventStore.EFCore.Providers;

namespace PureES.EventStore.EFCore;

internal class EventStoreDbContext : DbContext
{
    private readonly DbContextOptions _dbContextOptions;
    private readonly IOptions<EfCoreEventStoreOptions> _storeOptions;

    public EventStoreDbContext(
        DbContextOptions dbContextOptions,
        IOptions<EfCoreEventStoreOptions> storeOptions)
        : base(dbContextOptions)
    {
        _dbContextOptions = dbContextOptions;
        _storeOptions = storeOptions;
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
            "Npgsql.EntityFrameworkCore.PostgreSQL" => new PostgresProvider(this),
            null => throw new InvalidOperationException("Database provider not set"),
            _ => throw new NotImplementedException($"Database provider {Database.ProviderName} not supported")
        };
    }
    
    /// <summary>
    /// Cache key to use for model caching
    /// </summary>
    /// <returns></returns>
    public (string? Provider, string? Schema) GetModelCacheKey() => (Database.ProviderName, _storeOptions.Value.Schema);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, ProviderModelCacheKeyFactory>();
    }

    #endregion
    
    #region Events
    
    public async Task<List<EventStoreEvent>> WriteAndSaveChanges(List<EventStoreEvent> events, CancellationToken ct)
    {
        //Create a new context to avoid issues with tracking
        await using var context = new EventStoreDbContext(_dbContextOptions, _storeOptions);
        context.Set<EventStoreEvent>().AddRange(events);
        await context.SaveChangesAsync(ct);
        return events;
    }

    public IQueryable<EventStoreEvent> QueryEvents() => Set<EventStoreEvent>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (_storeOptions.Value.Schema != null)
            modelBuilder.HasDefaultSchema(_storeOptions.Value.Schema);
        
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

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(4096);

            entity.HasIndex(e => e.Timestamp); // For reading across multiple streams

            entity.OwnsMany(e => e.EventTypes, b =>
            {
                b.Property(x => x.TypeName)
                    .IsRequired()
                    .HasMaxLength(4096);
                
                // Event type filters use an exists query
                // Index on composite key and value
                // Composite key uses shadow properties
                b.HasIndex(
                    $"{nameof(EventStoreEvent)}{nameof(EventStoreEvent.StreamId)}",
                    $"{nameof(EventStoreEvent)}{nameof(EventStoreEvent.StreamPos)}",
                    nameof(EventType.TypeName));

            });

            Provider.ConfigureEntity(entity);
        });
    }
    
    #endregion
}

/// <summary>
/// A wrapper around <see cref="EventStoreDbContext"/>
/// that allows sharing the same options between multiple contexts.
/// </summary>
internal class EventStoreDbContext<TContext> : EventStoreDbContext where TContext : DbContext
{
    public EventStoreDbContext(
        DbContextOptions<TContext> dbContextOptions,
        IOptions<EfCoreEventStoreOptions> storeOptions)
        : base(new MirrorDbContextOptions<EventStoreDbContext>(dbContextOptions), storeOptions)
    {
    }
}