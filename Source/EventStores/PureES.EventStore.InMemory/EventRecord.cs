using System.Text.Json;

namespace PureES.EventStore.InMemory;

internal record EventRecord(string StreamId,
    int StreamPos,
    DateTime Timestamp,
    string EventType,
    JsonElement Event,
    JsonElement? Metadata)
{
    public int StreamPos { get; set; } = StreamPos;
}