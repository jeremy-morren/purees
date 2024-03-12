using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace PureES.EventStores.Marten;

/// <summary>
/// An event stored inside Marten
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
[Newtonsoft.Json.JsonConverter(typeof(NewtonsoftJsonEventConverter))]
public record MartenEvent(string StreamId, 
    int StreamPosition,
    string EventType,
    JsonElement? Event,
    JsonElement? Metadata)
{
    public string Id => $"{StreamId}/{StreamPosition}";
    
    [JsonIgnore] public DateTimeOffset Timestamp { get; set; }
}