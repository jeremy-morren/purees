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
    /// Read a timestamp from the data reader
    /// </summary>
    DateTime ReadTimestamp(DbDataReader reader, int ordinal);
}