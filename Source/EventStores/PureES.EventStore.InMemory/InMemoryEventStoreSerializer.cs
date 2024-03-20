﻿using System.Text.Json;
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

    public EventEnvelope Deserialize(EventRecord record)
    {
        var metadata = record.Metadata?.Deserialize(_options.MetadataType, _options.JsonSerializerOptions);
        
        var eventType = _typeMap.GetCLRType(record.EventType);
        var @event = record.Event.Deserialize(eventType, _options.JsonSerializerOptions) 
                     ?? throw new InvalidOperationException($"Event data is null for event {record.StreamId}/{record.StreamPos}");
        
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
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(record);

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

    public EventRecord Serialize(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var @event = JsonSerializer.SerializeToElement(envelope.Event, _options.JsonSerializerOptions);
        JsonElement? metadata = envelope.Metadata != null 
            ? JsonSerializer.SerializeToElement(envelope.Metadata, _options.JsonSerializerOptions)
            : null;
        return new EventRecord(envelope.StreamId,
            (int)envelope.StreamPosition,
            envelope.Timestamp,
            _typeMap.GetTypeName(envelope.Event.GetType()),
            @event,
            metadata);
    }
}