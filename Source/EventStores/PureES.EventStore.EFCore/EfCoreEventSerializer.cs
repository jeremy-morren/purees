using System.Text.Json;
using Microsoft.Extensions.Options;

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
        return new EventStoreEvent()
        {
            StreamId = streamId,
            StreamPos = streamPos,
            // Clone the list to avoid issues with EF Core
            EventTypes = _map.GetTypeNames(@event.Event.GetType()).ToList(),
            Data = JsonSerializer.SerializeToElement(@event.Event, _jsonOptions),
            Metadata = @event.Metadata != null 
                ? JsonSerializer.SerializeToElement(@event.Metadata, _jsonOptions) 
                : null,
        };
    }
    
    #region Deserialize

    public object DeserializeEvent(string streamId, int streamPos, string eventType, string data)
    {
        var type = _map.GetCLRType(eventType);
        try
        {
            return JsonSerializer.Deserialize(data, type, _jsonOptions)
                   ?? throw new Exception($"Event {streamId}/{streamPos} data is null");
        }
        catch (JsonException e)
        {
            throw new Exception($"Failed to deserialize event {streamId}/{streamPos} to {type}", e);
        }
    }
    
    public object? DeserializeMetadata(string streamId, int streamPos, string? metadata)
    {
        if (metadata == null)
            return null;
        try
        {
            return JsonSerializer.Deserialize(metadata, _metadataType, _jsonOptions);
        }
        catch (JsonException e)
        {
            throw new Exception($"Failed to deserialize metadata for event {streamId}/{streamPos} to {_metadataType}", e);
        }
    }
    
    #endregion
}