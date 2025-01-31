using System.Collections.Immutable;
using System.Text.Json;
using JetBrains.Annotations;

namespace PureES.EventStore.CosmosDB;

/// <summary>
/// An event stored inside Cosmos
/// </summary>
internal record CosmosEvent(
    DateTime Created,
    string EventStreamId,
    uint EventStreamPosition,
    ImmutableArray<string> EventTypes,
    JsonElement? Event,
    JsonElement? Metadata)
{
    [UsedImplicitly]
    public string Id => $"{EventStreamId}|{EventStreamPosition}";
    
    /// <summary>
    /// Actual (concrete) event type, used to simplify querying
    /// </summary>
    [UsedImplicitly]
    public string EventType => EventTypes[^1];
}