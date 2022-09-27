using System.Text.Json;
using EventStore.Client;
using PureES.Core;
using PureES.EventStoreDB;

namespace PureES.EventStore.Tests.Framework;

public class TestSerializer : IEventStoreDBSerializer
{
    public string GetTypeName(Type eventType) => eventType.Name;

    public EventEnvelope Deserialize(EventRecord record)
    {
        var @event = JsonSerializer.Deserialize<JsonElement>(record.Data.Span, JsonOptions);
        return new EventEnvelope(record.EventId.ToGuid(),
            record.EventStreamId,
            record.EventNumber.ToUInt64(),
            record.Created,
            @event!,
            null);
    }

    public EventData Serialize(UncommittedEvent @event) => new (Uuid.FromGuid(@event.EventId),
        @event.Event.GetType().Name,
        JsonSerializer.SerializeToUtf8Bytes(@event.Event),
        null);

    public EventData Serialize<T>(T @event) => throw new NotImplementedException();

    public T Deserialize<T>(EventRecord record) => JsonSerializer.Deserialize<T>(record.Data.Span, JsonOptions)!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}