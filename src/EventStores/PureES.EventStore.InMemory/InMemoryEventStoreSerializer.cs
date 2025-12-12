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

    public EventEnvelope Deserialize(InMemoryEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.EventTypes.Length == 0 || record.Timestamp.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException($"Invalid event record {record.StreamId}/{record.StreamPos}");

        return new EventEnvelope(
            record.StreamId,
            (uint)record.StreamPos,
            record.Timestamp,
            DeserializeEvent(record.StreamId, record.StreamPos, record.EventTypes[^1], record.Event),
            DeserializeMetadata(record.StreamId, record.StreamPos, record.Metadata));
    }

    private object DeserializeEvent(string streamId, int streamPos, string eventType, JsonElement @event)
    {
        try
        {
            var clrType = _typeMap.GetCLRType(eventType);
            return @event.Deserialize(clrType, _options.JsonOptions)
                   ?? throw new InvalidOperationException($"Event data is null for event {streamId}/{streamPos}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize event {streamId}/{streamPos}", ex);
        }
    }

    private object? DeserializeMetadata(string streamId, int streamPos, JsonElement? metadata)
    {
        try
        {
            return metadata?.Deserialize(_options.MetadataType, _options.JsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize event {streamId}/{streamPos}", ex);
        }
    }
}