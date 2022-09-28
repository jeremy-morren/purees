using System.Text.Json;
using EventStore.Client;
using PureES.Core;

namespace PureES.EventStoreDB.Serialization;

public class EventStoreDBSerializer<TMetadata> : IEventStoreDBSerializer
    where TMetadata : notnull
{
    private readonly JsonSerializerOptions _options;
    private readonly TypeMapper _typeMapper;

    public EventStoreDBSerializer(JsonSerializerOptions options,
        TypeMapper typeMapper)
    {
        _options = options;
        _typeMapper = typeMapper;
    }

    public string GetTypeName(Type eventType) => TypeMapper.GetString(eventType);

    public EventEnvelope Deserialize(EventRecord record)
    {
        var metadata = Deserialize<TMetadata>(record.Metadata);
        var @event = Deserialize(record.Data, record.EventType)
                     ?? throw new ArgumentException($"Event data is null for event {record.EventType}");
        return new EventEnvelope(record.EventId.ToGuid(),
            record.EventStreamId,
            record.EventNumber.ToUInt64(),
            record.Created,
            @event,
            metadata);
    }

    public EventData Serialize(UncommittedEvent @event) =>
        new(Uuid.FromGuid(@event.EventId),
            TypeMapper.GetString(@event.Event.GetType()),
            JsonSerializer.SerializeToUtf8Bytes(@event.Event, _options),
            JsonSerializer.SerializeToUtf8Bytes(@event.Metadata, _options));

    public EventData Serialize<T>(T @event) =>
        new(Uuid.NewUuid(),
            TypeMapper.GetString(typeof(T)),
            JsonSerializer.SerializeToUtf8Bytes(@event, _options));

    public T Deserialize<T>(EventRecord record) =>
        Deserialize<T>(record.Data) ?? throw new ArgumentException("Empty event");

    private T? Deserialize<T>(ReadOnlyMemory<byte> input) => JsonSerializer.Deserialize<T>(input.Span, _options);

    private object? Deserialize(ReadOnlyMemory<byte> input, string type) =>
        JsonSerializer.Deserialize(input.Span, _typeMapper.GetType(type), _options);
}