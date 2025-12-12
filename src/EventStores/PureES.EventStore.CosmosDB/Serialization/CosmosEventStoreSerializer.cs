using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PureES.EventStore.CosmosDB.Serialization;

internal class CosmosEventStoreSerializer
{
    private readonly IEventTypeMap _typeMap;
    private readonly CosmosEventStoreOptions _options;

    public CosmosEventStoreSerializer(IEventTypeMap typeMap, IOptions<CosmosEventStoreOptions> options)
    {
        _typeMap = typeMap;
        _options = options.Value;
    }

    public CosmosEvent Serialize(UncommittedEvent @event, string streamId, uint streamPosition, DateTimeOffset timestamp)
    {
        var e = JsonSerializer.SerializeToElement(@event.Event, _options.JsonSerializerOptions);
        var metadata = @event.Metadata != null
            ? JsonSerializer.SerializeToElement(@event.Metadata, _options.JsonSerializerOptions)
            : (JsonElement?)null;
        return new CosmosEvent(
            timestamp.UtcDateTime,
            streamId,
            streamPosition,
            _typeMap.GetTypeNames(@event.Event.GetType()),
            e,
            metadata);
    }

    public EventEnvelope Deserialize(CosmosEvent @event)
    {
        if (@event.EventTypes.Length == 0 || @event.Created.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException($"Invalid event record {@event.Id}");

        return new EventEnvelope(
            @event.EventStreamId,
            @event.EventStreamPosition,
            @event.Created,
            DeserializeEvent(@event.EventStreamId,
                @event.EventStreamPosition,
                @event.EventType,
                @event.Event),
            DeserializeMetadata(@event.EventStreamId,
                @event.EventStreamPosition,
                @event.Metadata));
    }

    private object DeserializeEvent(string streamId, uint streamPos, string eventType, JsonElement? json)
    {
        try
        {
            var clrType = _typeMap.GetCLRType(eventType);
            return json?.Deserialize(clrType, _options.JsonSerializerOptions)
                         ?? throw new InvalidOperationException($"Event data is null for event {streamId}/{streamPos}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize event {streamId}/{streamPos}",
                ex);
        }
    }

    private object? DeserializeMetadata(string streamId, uint streamPos, JsonElement? json)
    {
        try
        {
            return json?.Deserialize(_options.MetadataType, _options.JsonSerializerOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize metadata for event {streamId}/{streamPos}",
                ex);
        }
    }
}