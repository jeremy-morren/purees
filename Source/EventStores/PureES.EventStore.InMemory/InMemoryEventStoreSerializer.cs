using System.Runtime.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Options;

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

    public EventEnvelope Deserialize(InMemoryEventRecord record)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(record);
            if (record.EventTypes.Length == 0 || record.Timestamp.Kind != DateTimeKind.Utc)
                throw new InvalidOperationException($"Invalid event record {record.StreamId}/{record.StreamPos}");

            var metadata = record.Metadata?.Deserialize(_options.MetadataType, _options.JsonOptions);

            var eventType = _typeMap.GetCLRType(record.EventTypes[^1]);
            var @event = record.Event.Deserialize(eventType, _options.JsonOptions)
                         ?? throw new InvalidOperationException($"Event data is null for event {record.StreamId}/{record.StreamPos}");

            return new EventEnvelope(
                record.StreamId,
                (uint)record.StreamPos,
                record.Timestamp,
                @event,
                metadata);
        }
        catch (JsonException e)
        {
            throw new SerializationException($"Failed to deserialize event {record.StreamId}/{record.StreamPos}: {record.EventTypes[^1]}", e);
        }
    }

    public InMemoryEventRecord Serialize(UncommittedEvent record, string streamId, DateTimeOffset created)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(record);

        var @event = JsonSerializer.SerializeToElement(record.Event, _options.JsonOptions);
        JsonElement? metadata = record.Metadata != null 
            ? JsonSerializer.SerializeToElement(record.Metadata, _options.JsonOptions)
            : null;
        return new InMemoryEventRecord()
        {
            StreamId = streamId,
            Timestamp = created.UtcDateTime,
            EventTypes = _typeMap.GetTypeNames(record.Event.GetType()),
            Event = @event,
            Metadata = metadata
        };
    }

    public InMemoryEventRecord Serialize(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var @event = JsonSerializer.SerializeToElement(envelope.Event, _options.JsonOptions);
        JsonElement? metadata = envelope.Metadata != null 
            ? JsonSerializer.SerializeToElement(envelope.Metadata, _options.JsonOptions)
            : null;
        return new InMemoryEventRecord()
        {
            StreamId = envelope.StreamId,
            Timestamp = envelope.Timestamp,
            EventTypes = _typeMap.GetTypeNames(envelope.Event.GetType()),
            Event = @event,
            Metadata = metadata,
            StreamPos = (int)envelope.StreamPosition
        };
    }
}