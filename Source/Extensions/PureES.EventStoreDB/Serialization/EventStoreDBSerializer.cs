using EventStore.Client;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.Core.EventStore.Serialization;

namespace PureES.EventStoreDB.Serialization;

internal class EventStoreDBSerializer<TMetadata> : IEventStoreDBSerializer
    where TMetadata : notnull
{
    private readonly IEventStoreSerializer _serializer;
    private readonly IEventTypeMap _typeMap;

    public EventStoreDBSerializer(IEventTypeMap typeMap,
        IEventStoreSerializer serializer)
    {
        _typeMap = typeMap;
        _serializer = serializer;
    }

    public EventEnvelope Deserialize(EventRecord record)
    {
        const LazyThreadSafetyMode threadMode = LazyThreadSafetyMode.ExecutionAndPublication;
        var metadata = new Lazy<object?>(() => 
                record.Metadata.Length > 0 ? _serializer.Deserialize(record.Metadata.Span, typeof(TMetadata)) : null, 
            threadMode);
        var @event = new Lazy<object>(() =>
        {
            var type = _typeMap.TryGetType(record.EventType, out var eventType) ? eventType : null;
            return _serializer.Deserialize(record.Data.Span, type) 
                   ?? throw new ArgumentException($"Event data is null for event {record.EventType}");
        }, threadMode);
        return new EventEnvelope(record.EventId.ToGuid(),
            record.EventStreamId,
            record.EventNumber.ToUInt64(),
            record.Created,
            @event,
            metadata);
    }

    public EventData Serialize(UncommittedEvent record)
    {
        var @event = _serializer.Serialize(record.Event, out var contentType);
        var metadata = record.Metadata != null ? _serializer.Serialize(record.Metadata, out _) : null;
        return new EventData(Uuid.FromGuid(record.EventId),
            _typeMap.GetTypeName(record.Event.GetType()),
            @event,
            metadata,
            contentType);
    }
}