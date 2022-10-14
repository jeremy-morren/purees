using System.Text.Json;
using EventStore.Client;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.Core.EventStore.Serialization;

namespace PureES.EventStoreDB.Serialization;

internal class EventStoreDBSerializer<TMetadata> : IEventStoreDBSerializer
    where TMetadata : notnull
{
    private readonly IEventTypeMap _typeMap;
    private readonly IEventStoreSerializer _serializer;

    public EventStoreDBSerializer(IEventTypeMap typeMap,
        IEventStoreSerializer serializer)
    {
        _typeMap = typeMap;
        _serializer = serializer;
    }

    public EventEnvelope Deserialize(EventRecord record)
    {
        var metadata = record.Metadata.Length > 0 ? _serializer.Deserialize(record.Metadata.Span, typeof(TMetadata)) : null;
        var @event = _serializer.Deserialize(record.Data.Span, 
                _typeMap.TryGetType(record.EventType, out var eventType) ? eventType : null)
                     ?? throw new ArgumentException($"Event data is null for event {record.EventType}");
        return new EventEnvelope(record.EventId.ToGuid(),
            record.EventStreamId,
            record.EventNumber.ToUInt64(),
            record.Position.CommitPosition,
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