using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// ReSharper disable UnusedMember.Global

namespace PureES.CosmosDB;

/// <summary>
/// An event stored inside Cosmos
/// </summary>
[JsonSerializable(typeof(CosmosEvent))]
internal record CosmosEvent(Guid EventId,
    DateTime Created,
    string EventStreamId,
    ulong EventStreamPosition,
    string EventType,
    JsonNode? Event,
    JsonNode? Metadata)
{
    public string Id => $"{EventStreamId}|{EventStreamPosition}";
}