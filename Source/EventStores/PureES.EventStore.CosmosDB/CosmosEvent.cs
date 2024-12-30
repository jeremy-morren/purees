using System.Text.Json;
using JetBrains.Annotations;

namespace PureES.EventStore.CosmosDB;

/// <summary>
/// An event stored inside Cosmos
/// </summary>
internal record CosmosEvent(
    DateTime Created,
    string EventStreamId,
    ulong EventStreamPosition,
    List<string> EventType,
    JsonElement? Event,
    JsonElement? Metadata)
{
    [UsedImplicitly]
    public string Id => $"{EventStreamId}|{EventStreamPosition}";
}