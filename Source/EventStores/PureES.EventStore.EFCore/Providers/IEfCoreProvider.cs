using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PureES.EventStore.EFCore.Models;

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
    /// Returns true if the exception indicates that the entity already exists
    /// </summary>
    bool IsUniqueConstraintFailedException(DbException e);
    
    /// <summary>
    /// Read events from the database
    /// </summary>
    IAsyncEnumerable<EventEnvelope> ReadEvents(IQueryable<EventStoreEvent> queryable, EfCoreEventSerializer serializer, CancellationToken ct);
    
    /// <summary>
    /// Read events from the database
    /// </summary>
    IAsyncEnumerable<EventEnvelope> ReadEvents(DbCommand command, EfCoreEventSerializer serializer, CancellationToken ct);
    

    #region Queryable Helpers
    
    /// <summary>
    /// Implementation of count by event type
    /// </summary>
    IQueryable<long> CountByEventType(List<string> eventTypes);
    
    /// <summary>
    /// Implementation of count by event type
    /// </summary>
    DbCommand FilterByEventType(IQueryable<EventStoreEvent> query, List<string> eventTypes, ulong? maxCount);
    
    #endregion
}