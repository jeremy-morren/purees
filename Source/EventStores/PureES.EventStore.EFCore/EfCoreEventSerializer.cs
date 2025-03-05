using System.Text.Json;
using Microsoft.Extensions.Options;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore;

internal class EfCoreEventSerializer
{
    private readonly Type _metadataType;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IEventTypeMap _map;

    public EfCoreEventSerializer(IOptions<EfCoreEventStoreOptions> options, IEventTypeMap map)
    {
        _jsonOptions = options.Value.JsonOptions;
        _metadataType = options.Value.MetadataType;
        _map = map;
    }
    
    public EventStoreEvent Serialize(string streamId, int streamPos, UncommittedEvent @event)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfNegative(streamPos);

        return new EventStoreEvent()
        {
            StreamId = streamId,
            StreamPos = streamPos,
            // Clone the list to avoid issues with EF Core
            EventTypes = EventType.New(@event.Event.GetType(), _map),
            Event = JsonSerializer.SerializeToElement(@event.Event, _jsonOptions),
            Metadata = @event.Metadata != null 
                ? JsonSerializer.SerializeToElement(@event.Metadata, _jsonOptions) 
                : null,
        };
    }
    
    #region Deserialize string

    public object DeserializeEvent(string streamId, int streamPos, string eventType, string json)
    {
        var type = _map.GetCLRType(eventType);
        try
        {
            return JsonSerializer.Deserialize(json, type, _jsonOptions)
                   ?? throw new Exception($"Event {streamId}/{streamPos} data is null");
        }
        catch (JsonException e)
        {
            throw new Exception($"Failed to deserialize event {streamId}/{streamPos} to {type}", e);
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
        catch (JsonException e)
        {
            throw new Exception($"Failed to deserialize metadata for event {streamId}/{streamPos} to {_metadataType}", e);
        }
    }

    #endregion

    #region Deserialize Element


    public object DeserializeEvent(string streamId, int streamPos, string eventType, JsonElement json)
    {
        var type = _map.GetCLRType(eventType);
        try
        {
            return json.Deserialize(type, _jsonOptions)
                   ?? throw new Exception($"Event {streamId}/{streamPos} data is null");
        }
        catch (JsonException e)
        {
            throw new Exception($"Failed to deserialize event {streamId}/{streamPos} to {type}", e);
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
        catch (JsonException e)
        {
            throw new Exception($"Failed to deserialize metadata for event {streamId}/{streamPos} to {_metadataType}", e);
        }
    }

    #endregion
}