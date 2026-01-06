using System.Text.Json;

namespace PureES.EventStore.EFCore.Models;

/// <summary>
/// An event stored in the event store
/// </summary>
internal record EventStoreEvent
{
    /// <summary>
    /// The stream id of the event
    /// </summary>
    public required string StreamId { get; init; }
    
    /// <summary>
    /// The stream position of the event
    /// </summary>
    public required int StreamPos { get; init; }

    /// <summary>
    /// Timestamp of the event in UTC
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Index of the event in a transaction, if this event was added as part of a transaction
    /// </summary>
    /// <remarks>
    /// This allows reading events in the order they were added in a transaction
    /// </remarks>
    public required int? TransactionIndex { get; init; }

    /// <summary>
    /// The event type of the event (full hierarchy)
    /// </summary>
    public required List<EventType> EventTypes { get; init; }

    /// <summary>
    /// The concrete event type (last part)
    /// </summary>
    /// <remarks>
    /// When reading events, we can read this column (instead of referencing the EventTypes table)
    /// </remarks>
    public string EventType
    {
        get => EventTypes[^1].TypeName;
        // ReSharper disable once ValueParameterNotUsed
        // ReSharper disable once UnusedMember.Local
        private set {}
    }

    /// <summary>
    /// Event data
    /// </summary>
    public required JsonElement Event { get; init; }
    
    /// <summary>
    /// Event metadata, if any
    /// </summary>
    public JsonElement? Metadata { get; init; }
}