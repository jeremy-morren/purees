using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PureES.EventStores.Marten;

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
        var metadata = martenEvent.Metadata?.Deserialize(_options.MetadataType, _options.JsonSerializerOptions);
        var eventType = _typeMap.GetCLRType(martenEvent.EventType);
        var @event = martenEvent.Event?.Deserialize(eventType, _options.JsonSerializerOptions) 
                     ?? throw new InvalidOperationException($"Event data is null for event {martenEvent.Id}");
        return new EventEnvelope(martenEvent.StreamId,
            (ulong)martenEvent.StreamPosition,
            martenEvent.Timestamp.UtcDateTime,
            @event,
            metadata);
    }

    public MartenEvent Serialize(UncommittedEvent @event, string streamId, ulong streamPosition)
    {
        var e = JsonSerializer.SerializeToElement(@event.Event, _options.JsonSerializerOptions);
        var metadata = @event.Metadata != null 
            ? JsonSerializer.SerializeToElement(@event.Metadata, _options.JsonSerializerOptions) 
            : (JsonElement?)null;
        return new MartenEvent(streamId,
            (int)streamPosition,
            _typeMap.GetTypeName(@event.Event.GetType()),
            e,
            metadata);
    }
}