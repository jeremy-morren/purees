using System.Text.Json;
using Microsoft.Extensions.Options;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore;

internal class EfCoreEventSerializer
{
    private readonly Type _metadataType;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IEventTypeMap _typeMap;

    public EfCoreEventSerializer(IOptions<EfCoreEventStoreOptions> options, IEventTypeMap typeMap)
    {
        _jsonOptions = options.Value.JsonOptions;
        _metadataType = options.Value.MetadataType;
        _typeMap = typeMap;
    }
    
    public EventStoreEvent Serialize(string streamId, int streamPos, int? transactionIndex, UncommittedEvent @event)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfNegative(streamPos);

        // Clone the list to avoid issues with EF Core
        var eventTypes = EventType.New(@event.Event.GetType(), _typeMap);

        return new EventStoreEvent()
        {
            StreamId = streamId,
            StreamPos = streamPos,
            TransactionIndex = transactionIndex,

            EventTypes = eventTypes,
            EventType = eventTypes[^1].TypeName,

            Event = JsonSerializer.SerializeToElement(@event.Event, _jsonOptions),
            Metadata = @event.Metadata != null 
                ? JsonSerializer.SerializeToElement(@event.Metadata, _jsonOptions) 
                : null,
        };
    }
    
    #region Deserialize string

    public object DeserializeEvent(string streamId, int streamPos, string eventType, string json)
    {
        try
        {
            var type = _typeMap.GetCLRType(eventType);
            return JsonSerializer.Deserialize(json, type, _jsonOptions)
                   ?? throw new Exception($"Event {streamId}/{streamPos} data is null");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize event {streamId}/{streamPos}", ex);
        }
    }
    
    public object? DeserializeMetadata(string streamId, int streamPos, string? json)
    {
        if (json == null)
            return null;
        try
        {
            return JsonSerializer.Deserialize(json, _metadataType, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize metadata for event {streamId}/{streamPos} to {_metadataType}", ex);
        }
    }

    #endregion

    #region Deserialize Element

    public object DeserializeEvent(string streamId, int streamPos, string eventType, JsonElement @event)
    {
        Type clrType;
        try
        {
            clrType = _typeMap.GetCLRType(eventType);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to determine CLR type for event {streamId}/{streamPos}", ex);
        }

        try
        {
            return @event.Deserialize(clrType, _jsonOptions)
                   ?? throw new Exception($"Event {streamId}/{streamPos} data is null");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize event {streamId}/{streamPos} to {clrType}", ex);
        }
    }

    public object? DeserializeMetadata(string streamId, int streamPos, JsonElement? json)
    {
        if (json == null)
            return null;
        try
        {
            return json.Value.Deserialize(_metadataType, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize metadata for event {streamId}/{streamPos} to {_metadataType}", ex);
        }
    }

    #endregion
}