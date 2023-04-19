using System.Text.Json;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStore.InMemory.Serialization;

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
        const LazyThreadSafetyMode threadMode = LazyThreadSafetyMode.ExecutionAndPublication;
        var metadata = new Lazy<object?>(() => 
            record.Metadata != null 
                ? JsonSerializer.Deserialize(record.Metadata, _options.MetadataType, _options.JsonSerializerOptions)
                : null, 
            threadMode);
        var @event = new Lazy<object>(() =>
            {
                var type = _typeMap.GetCLRType(record.EventType);
                return JsonSerializer.Deserialize(record.Event, type, _options.JsonSerializerOptions)
                       ?? throw new InvalidOperationException($"Event data is null for event {record.EventId}");
            }, 
            threadMode);
        return new EventEnvelope(record.EventId,
            record.StreamId,
            record.StreamPosition,
            record.Created,
            @event,
            metadata);
    }

    public EventRecord Serialize(UncommittedEvent record, string streamId, DateTimeOffset created)
    {
        var @event = JsonSerializer.SerializeToUtf8Bytes(record.Event, _options.JsonSerializerOptions);
        var metadata = record.Metadata != null 
            ? JsonSerializer.SerializeToUtf8Bytes(record.Metadata, _options.JsonSerializerOptions)
            : null;
        return new EventRecord
        {
            StreamId = streamId,
            EventId = record.EventId,
            Created = created.UtcDateTime,
            EventType = _typeMap.GetTypeName(record.Event.GetType()),
            Event = @event,
            Metadata = metadata
        };
    }
}