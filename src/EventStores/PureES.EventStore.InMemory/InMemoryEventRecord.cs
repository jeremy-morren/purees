using System.Collections.Immutable;
using System.Text.Json;

namespace PureES.EventStore.InMemory;

/// <summary>
/// An event stored in memory
/// </summary>
public record InMemoryEventRecord
{
    /// <summary>
    /// Stream position
    /// </summary>
    public int StreamPos { get; set; }
    
    /// <summary>
    /// Stream Id
    /// </summary>
    public required string StreamId { get; init; }
    
    /// <summary>
    /// Timestamp of the event (UTC)
    /// </summary>
    public required DateTime Timestamp { get; init; }
    
    /// <summary>
    /// Event type (full inheritance hierarchy)
    /// </summary>
    public required ImmutableArray<string> EventTypes { get; init; }
    
    /// <summary>
    /// Event data
    /// </summary>
    public required JsonElement Event { get; init; }
    
    /// <summary>
    /// Event metadata
    /// </summary>
    public required JsonElement? Metadata { get; init; }

    /// <summary>
    /// Whether the event type contains any of the provided types
    /// </summary>
    public bool TypeContains(HashSet<string> types) => types.Overlaps(EventTypes);
}