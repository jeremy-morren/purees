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

    public EventStoreEvent Serialize(string streamId, uint streamPos, UncommittedEvent @event)
    {
        return new EventStoreEvent()
        {
            StreamId = streamId,
            StreamPos = streamPos,
            EventTypes = _map.GetTypeNames(@event.GetType()),
            Data = JsonSerializer.SerializeToElement(@event.Event, _jsonOptions),
            Metadata = @event.Metadata != null 
                ? JsonSerializer.SerializeToElement(@event.Metadata, _jsonOptions) 
                : null,
        };
    }

    public EventEnvelope Deserialize(EventStoreEvent @event)
    {
        var data = DeserializeData(@event);
        var metadata = @event.Metadata?.Deserialize(_metadataType, _jsonOptions);
        return new EventEnvelope(@event.StreamId, @event.StreamPos, @event.Timestamp, data, metadata);
    }

    private object DeserializeData(EventStoreEvent @event)
    {
        var type = _map.GetCLRType(@event.EventTypes[^1]);
        try
        {
            return @event.Data.Deserialize(type, _jsonOptions)
                ?? throw new Exception($"Event {@event.StreamId}/{@event.StreamPos} data is null");
        }
        catch (JsonException e)
        {
            throw new Exception($"Failed to deserialize event {@event.StreamId}/{@event.StreamPos} to {type}", e);
        }
    }
}