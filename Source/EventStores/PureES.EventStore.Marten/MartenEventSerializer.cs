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

    public EventEnvelope Deserialize(MartenEvent martenEvent)
    {
        if (martenEvent.EventTypes == null!)
        {
            // Old event, migrate
            martenEvent = martenEvent with
            {
#pragma warning disable CS0618 // Type or member is obsolete
                EventTypes = [martenEvent.EventType!]
#pragma warning restore CS0618 // Type or member is obsolete
            };
        }
        if (martenEvent.EventTypes.Length == 0)
            throw new InvalidOperationException($"Invalid event record {martenEvent.Id}");
        
        var metadata = Deserialize(martenEvent.Metadata, _options.MetadataType);
        var eventType = _typeMap.GetCLRType(martenEvent.EventTypes[^1]);
        
        var @event = Deserialize(martenEvent.Event, eventType)
                     ?? throw new InvalidOperationException($"Event data is null for event {martenEvent.Id}");
        return new EventEnvelope(martenEvent.StreamId,
            (uint)martenEvent.StreamPosition,
            martenEvent.Timestamp.UtcDateTime,
            @event,
            metadata);
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
    
    private object? Deserialize(JsonElement? element, Type type)
    {
        if (element == null)
            return null;
        try
        {
            return element.Value.Deserialize(type, _options.JsonSerializerOptions);
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"Failed to deserialize JSON element to type {type}", e);
        }
    }
}