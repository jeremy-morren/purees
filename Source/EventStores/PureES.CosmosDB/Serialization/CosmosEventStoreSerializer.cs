using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.CosmosDB.Serialization;

internal class CosmosEventStoreSerializer : ICosmosEventStoreSerializer
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
        const LazyThreadSafetyMode threadMode = LazyThreadSafetyMode.ExecutionAndPublication;
        var metadata = new Lazy<object?>(() => 
                cosmosEvent.Metadata.Deserialize(_options.MetadataType, _options.JsonSerializerOptions), 
            threadMode);
        var @event = new Lazy<object>(() =>
        {
            var type = _typeMap.GetCLRType(cosmosEvent.EventType);
            return cosmosEvent.Event.Deserialize(type, _options.JsonSerializerOptions)
                   ?? throw new InvalidOperationException($"Event data is null for event {cosmosEvent.EventId}");
        }, threadMode);
        return new EventEnvelope(cosmosEvent.EventId,
            cosmosEvent.EventStreamId,
            cosmosEvent.EventStreamPosition,
            cosmosEvent.Created,
            @event,
            metadata);
    }

    public CosmosEvent Serialize(UncommittedEvent @event, string streamId, ulong streamPosition, DateTimeOffset timestamp)
    {
        var e = JsonSerializer.SerializeToNode(@event.Event, _options.JsonSerializerOptions);
        var metadata = @event.Metadata != null 
            ? JsonSerializer.SerializeToNode(@event.Metadata, _options.JsonSerializerOptions) 
            : null;
        return new CosmosEvent(@event.EventId,
            timestamp.UtcDateTime,
            streamId,
            streamPosition,
            _typeMap.GetTypeName(@event.Event.GetType()),
            e,
            metadata);
    }
    
    #region Public

    public EventEnvelope Deserialize(JToken item) =>
        Deserialize(item.ToObject<CosmosEvent>() ?? throw new ArgumentNullException(nameof(item)));

    public EventEnvelope Deserialize(JsonElement item) => 
        Deserialize(item.Deserialize<CosmosEvent>() ?? throw new ArgumentNullException(nameof(item)));

    #endregion
}