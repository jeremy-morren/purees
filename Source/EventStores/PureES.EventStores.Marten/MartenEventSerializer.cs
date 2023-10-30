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
        const LazyThreadSafetyMode threadMode = LazyThreadSafetyMode.ExecutionAndPublication;
        var metadata = new Lazy<object?>(() => 
                marvenEvent.Metadata?.Deserialize(_options.MetadataType, _options.JsonSerializerOptions), 
            threadMode);
        var @event = new Lazy<object>(() =>
        {
            var type = _typeMap.GetCLRType(marvenEvent.EventType);
            return marvenEvent.Event?.Deserialize(type, _options.JsonSerializerOptions)
                   ?? throw new InvalidOperationException($"Event data is null for event {marvenEvent.Id}");
        }, threadMode);
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