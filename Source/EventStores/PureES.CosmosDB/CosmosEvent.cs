using System.Text.Json;
using System.Text.Json.Serialization;

namespace PureES.CosmosDB;

/// <summary>
/// An event which has not been committed
/// </summary>
[JsonSerializable(typeof(CosmosEvent))]
internal record CosmosEvent(Guid EventId,
    DateTime Created,
    string EventStreamId,
    ulong EventStreamPosition,
    string EventType,
    JsonElement Event,
    JsonElement? Metadata)
{
    public string Id => $"{EventStreamId}|{EventStreamPosition}";
}