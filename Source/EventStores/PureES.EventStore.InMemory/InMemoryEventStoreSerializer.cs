using System.Text.Json;
using Microsoft.Extensions.Options;
using PureES.Core;

namespace PureES.EventStore.InMemory;

internal class InMemoryEventStoreSerializer
{
    private readonly IEventTypeMap _typeMap;
    private readonly InMemoryEventStoreOptions _options;

    public InMemoryEventStoreSerializer(IEventTypeMap typeMap,
        IOptions<InMemoryEventStoreOptions> options)
    {
        _typeMap = typeMap;
        _options = options.Value;
    }

    public EventEnvelope Deserialize(EventRecord record)
    {
        var metadata = new Lazy<object?>(() => 
            record.Metadata?.Deserialize(_options.MetadataType, _options.JsonSerializerOptions),
            true);
        var @event = new Lazy<object>(() =>
            {
                var type = _typeMap.GetCLRType(record.EventType);
                return record.Event.Deserialize(type, _options.JsonSerializerOptions)
                       ?? throw new InvalidOperationException($"Event data is null for event {record.StreamId}/{record.StreamPos}");
            }, 
            true);
        return new EventEnvelope(record.StreamId,
            (ulong)record.StreamPos,
            record.Timestamp,
            @event,
            metadata);
    }

    public EventRecord Serialize(UncommittedEvent record, 
        string streamId, 
        int streamPos,
        DateTimeOffset created)
    {
        var @event = JsonSerializer.SerializeToElement(record.Event, _options.JsonSerializerOptions);
        JsonElement? metadata = record.Metadata != null 
            ? JsonSerializer.SerializeToElement(record.Metadata, _options.JsonSerializerOptions)
            : null;
        return new EventRecord(streamId,
            streamPos,
            created.UtcDateTime,
            _typeMap.GetTypeName(record.Event.GetType()),
            @event,
            metadata);
    }
}