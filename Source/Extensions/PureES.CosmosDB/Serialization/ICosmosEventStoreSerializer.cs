using System.Text.Json;
using Newtonsoft.Json.Linq;
using PureES.Core;

namespace PureES.CosmosDB.Serialization;

public interface ICosmosEventStoreSerializer
{
    /// <summary>
    /// Deserializes an EventEnvelope
    /// </summary>
    /// <param name="item">CosmosDB envelope to deserialize</param>
    /// <returns></returns>
    EventEnvelope Deserialize(JToken item);

    /// <summary>
    /// Deserializes an EventEnvelope
    /// </summary>
    /// <param name="item">CosmosDB envelope to deserialize</param>
    /// <returns></returns>
    EventEnvelope Deserialize(JsonElement item);
}