using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PureES.EventStore.EFCore.Providers;

/// <summary>
/// A provider for a specific database provider
/// </summary>
internal interface IEfCoreProvider
{
    /// <summary>
    /// Configure the model builder
    /// </summary>
    void ConfigureEntity(EntityTypeBuilder<EventStoreEvent> builder);
    
    /// <summary>
    /// Write the events to the event store
    /// </summary>
    Task<List<EventStoreEvent>> WriteEvents(IEnumerable<EventStoreEvent> events, CancellationToken ct);
    
    /// <summary>
    /// Read events from the database
    /// </summary>
    IAsyncEnumerable<EventEnvelope> ReadEvents(IQueryable<EventStoreEvent> query, EfCoreEventSerializer serializer,  CancellationToken ct);
    
    /// <summary>
    /// Returns true if the exception indicates that the entity already exists
    /// </summary>
    bool IsAlreadyExistsException(DbException e);
}