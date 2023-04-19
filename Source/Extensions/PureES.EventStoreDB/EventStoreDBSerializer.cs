using System.Text.Json;
using EventStore.Client;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStoreDB;

internal class EventStoreDBSerializer
{
    private readonly IEventTypeMap _typeMap;
    private readonly EventStoreDBOptions _options;

    public EventStoreDBSerializer(IEventTypeMap typeMap,
        IOptions<EventStoreDBOptions> options)
    {
        _typeMap = typeMap;
        _options = options.Value;
    }

    public EventEnvelope Deserialize(EventRecord record)
    {
        const LazyThreadSafetyMode threadMode = LazyThreadSafetyMode.ExecutionAndPublication;
        var metadata = new Lazy<object?>(() => 
                record.Metadata.Length > 0 
                    ? JsonSerializer.Deserialize(record.Metadata.Span, _options.MetadataType, _options.JsonSerializerOptions)
                    : null, 
            threadMode);
        var @event = new Lazy<object>(() =>
        {
            var type = _typeMap.GetCLRType(record.EventType);
            return JsonSerializer.Deserialize(record.Data.Span, type, _options.JsonSerializerOptions)
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
        var @event = JsonSerializer.SerializeToUtf8Bytes(record.Event, _options.JsonSerializerOptions);
        var metadata = record.Metadata != null 
            ? JsonSerializer.SerializeToUtf8Bytes(record.Metadata, _options.JsonSerializerOptions)
            : null;
        return new EventData(Uuid.FromGuid(record.EventId),
            _typeMap.GetTypeName(record.Event.GetType()),
            @event,
            metadata);
    }
}