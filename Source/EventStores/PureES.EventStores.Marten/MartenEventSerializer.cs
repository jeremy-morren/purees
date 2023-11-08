using System.Text.Json;
using Microsoft.Extensions.Options;
using PureES.Core;

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

    public EventEnvelope Deserialize(MartenEvent marvenEvent)
    {
        var metadata = marvenEvent.Metadata?.Deserialize(_options.MetadataType, _options.JsonSerializerOptions);
        var eventType = _typeMap.GetCLRType(marvenEvent.EventType);
        var @event = marvenEvent.Event?.Deserialize(eventType, _options.JsonSerializerOptions) 
                     ?? throw new InvalidOperationException($"Event data is null for event {marvenEvent.Id}");
        return new EventEnvelope(marvenEvent.StreamId,
            (ulong)marvenEvent.StreamPosition,
            marvenEvent.Timestamp.UtcDateTime,
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