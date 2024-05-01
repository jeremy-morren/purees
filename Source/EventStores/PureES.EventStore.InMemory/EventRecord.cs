using System.Text.Json;

namespace PureES.EventStore.InMemory;

internal record EventRecord
{
    public int StreamPos { get; set; }
    public required string StreamId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string EventType { get; init; }
    public required JsonElement Event { get; init; }
    public required JsonElement? Metadata { get; init; }
}