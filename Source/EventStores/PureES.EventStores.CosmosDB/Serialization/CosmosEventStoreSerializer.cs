using System.Text.Json;
using Microsoft.Extensions.Options;
using PureES.Core;

namespace PureES.EventStores.CosmosDB.Serialization;

internal class CosmosEventStoreSerializer
{
    private readonly IEventTypeMap _typeMap;
    private readonly CosmosEventStoreOptions _options;

    public CosmosEventStoreSerializer(IEventTypeMap typeMap, IOptions<CosmosEventStoreOptions> options)
    {
        _typeMap = typeMap;
        _options = options.Value;
    }

    public EventEnvelope Deserialize(CosmosEvent cosmosEvent)
    {
        var metadata = cosmosEvent.Metadata?.Deserialize(_options.MetadataType, _options.JsonSerializerOptions);
        
        var eventType = _typeMap.GetCLRType(cosmosEvent.EventType);
        var @event = cosmosEvent.Event?.Deserialize(eventType, _options.JsonSerializerOptions) 
                     ?? throw new InvalidOperationException($"Event data is null for event {cosmosEvent.Id}");
        return new EventEnvelope(cosmosEvent.EventStreamId,
            cosmosEvent.EventStreamPosition,
            cosmosEvent.Created,
            @event,
            metadata);
    }

    public CosmosEvent Serialize(UncommittedEvent @event, string streamId, ulong streamPosition, DateTimeOffset timestamp)
    {
        var e = JsonSerializer.SerializeToElement(@event.Event, _options.JsonSerializerOptions);
        var metadata = @event.Metadata != null 
            ? JsonSerializer.SerializeToElement(@event.Metadata, _options.JsonSerializerOptions) 
            : (JsonElement?)null;
        return new CosmosEvent(timestamp.UtcDateTime,
            streamId,
            streamPosition,
            _typeMap.GetTypeName(@event.Event.GetType()),
            e,
            metadata);
    }
}