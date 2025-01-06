using System.Text.Json;

namespace PureES.EventStore.EFCore;

/// <summary>
/// An event stored in the event store
/// </summary>
internal class EventStoreEvent
{
    /// <summary>
    /// The stream id of the event
    /// </summary>
    public required string StreamId { get; init; }
    
    /// <summary>
    /// The stream position of the event
    /// </summary>
    public required uint StreamPos { get; init; }

    /// <summary>
    /// Timestamp of the event in UTC
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// The event type of the event (full hierarchy)
    /// </summary>
    public required List<string> EventTypes { get; init; }
    
    /// <summary>
    /// Event data
    /// </summary>
    public required JsonElement Data { get; init; }
    
    /// <summary>
    /// Event metadata, if any
    /// </summary>
    public JsonElement? Metadata { get; init; }
}