using PureES.Core;
using PureES.Core.EventStore;
using PureES.Core.EventStore.Serialization;

namespace PureES.EventStore.InMemory.Serialization;

internal class InMemoryEventStoreSerializer<TMetadata> : IInMemoryEventStoreSerializer
    where TMetadata : notnull
{
    private readonly IEventTypeMap _typeMap;
    private readonly IEventStoreSerializer _serializer;

    public InMemoryEventStoreSerializer(IEventTypeMap typeMap,
        IEventStoreSerializer serializer)
    {
        _typeMap = typeMap;
        _serializer = serializer;
    }

    public EventEnvelope Deserialize(EventRecord record)
    {
        var metadata = record.Metadata != null ? _serializer.Deserialize(record.Metadata, typeof(TMetadata)) : null;
        var @event = _serializer.Deserialize(record.Data, 
                _typeMap.TryGetType(record.EventType, out var eventType) ? eventType : null)
                     ?? throw new ArgumentException($"Event data is null for event {record.EventType}");
        return new EventEnvelope(record.EventId,
            record.StreamId,
            record.StreamPosition,
            record.OverallPosition,
            record.Created,
            @event,
            metadata);
    }

    public EventRecord Serialize(UncommittedEvent record, string streamId, DateTimeOffset created)
    {
        var @event = _serializer.Serialize(record.Event, out var contentType);
        var metadata = record.Metadata != null ? _serializer.Serialize(record.Metadata, out _) : null;
        return new EventRecord()
        {
            StreamId = streamId,
            EventId = record.EventId,
            Created = created.UtcDateTime,
            EventType = _typeMap.GetTypeName(record.Event.GetType()),
            Data = @event,
            Metadata = metadata,
            ContentType = contentType
        };
    }
}