using System.Text.Json;
using System.Text.Json.Nodes;

namespace PureES.EventStore.EFCore;

/// <summary>
/// An event stored in the event store
/// </summary>
internal class EventStoreEvent
{
    /// <summary>
    /// The stream id of the event
    /// </summary>
    public required string StreamId { get; set; }
    
    /// <summary>
    /// The stream position of the event
    /// </summary>
    public required uint StreamPos { get; set; }

    /// <summary>
    /// Timestamp of the event in UTC
    /// </summary>
    /// <remarks>
    /// It is set here to allow in memory and sqlite databases. For other databases, it is generated on the server
    /// </remarks>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// The event type of the event (full hierarchy)
    /// </summary>
    public required List<string> EventTypes { get; set; }
    
    /// <summary>
    /// Event data
    /// </summary>
    public JsonElement? Data { get; set; }
    
    /// <summary>
    /// Event metadata, if any
    /// </summary>
    public JsonElement? Metadata { get; set; }
}