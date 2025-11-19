using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PureES.EventStore.Marten;

internal class MartenEventSerializer
{
    private readonly IEventTypeMap _typeMap;
    private readonly MartenEventStoreOptions _options;

    public MartenEventSerializer(IEventTypeMap typeMap, IOptions<MartenEventStoreOptions> options)
    {
        _typeMap = typeMap;
        _options = options.Value;
    }

    public MartenEvent Serialize(UncommittedEvent @event, string streamId, uint streamPosition)
    {
        var e = JsonSerializer.SerializeToElement(@event.Event, _options.JsonSerializerOptions);
        var metadata = @event.Metadata != null
            ? JsonSerializer.SerializeToElement(@event.Metadata, _options.JsonSerializerOptions)
            : (JsonElement?)null;

        return new MartenEvent(streamId,
            (int)streamPosition,
            _typeMap.GetTypeNames(@event.Event.GetType()).ToArray(),
            e,
            metadata);
    }

    public EventEnvelope Deserialize(MartenEvent @event)
    {
        if (@event.EventTypes == null!)
        {
            // Old event, migrate
            @event = @event with
            {
#pragma warning disable CS0618 // Type or member is obsolete
                EventTypes = [@event.EventType!]
#pragma warning restore CS0618 // Type or member is obsolete
            };
        }
        if (@event.EventTypes.Length == 0)
            throw new InvalidOperationException($"Invalid event record {@event.Id}");

        return new EventEnvelope(
            @event.StreamId,
            (uint)@event.StreamPosition,
            @event.Timestamp.UtcDateTime,
            DeserializeEvent(
                @event.StreamId,
                @event.StreamPosition,
                @event.EventTypes[^1],
                @event.Event),
            DeserializeMetadata(
                @event.StreamId,
                @event.StreamPosition,
                @event.Metadata));
    }

    private object DeserializeEvent(string streamId, int streamPos, string eventType, JsonElement? json)
    {
        try
        {
            var clrType = _typeMap.GetCLRType(eventType);
            return json?.Deserialize(clrType, _options.JsonSerializerOptions)
                   ?? throw new Exception($"Event {streamId}/{streamPos} data is null");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize event {streamId}/{streamPos}", ex);
        }
    }

    private object? DeserializeMetadata(string streamId, int streamPos, JsonElement? json)
    {
        try
        {
            return json?.Deserialize(_options.MetadataType, _options.JsonSerializerOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to metadata for event {streamId}/{streamPos} to {_options.MetadataType}", ex);
        }
    }
}