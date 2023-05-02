using System.Text.Json;
using Azure.Core.Serialization;

namespace PureES.CosmosDB.Serialization;

/// <summary>
/// Uses <see cref="Azure.Core.Serialization.JsonObjectSerializer"/> which leverages System.Text.Json providing a simple API to interact with on the Azure SDKs.
/// </summary>
// <SystemTextJsonSerializer>
internal class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonObjectSerializer _serializer;

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions options) 
        => _serializer = new JsonObjectSerializer(options);

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream is {CanSeek: true, Length: 0})
                return default!;

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
                return (T)(object)stream;

            return (T)_serializer.Deserialize(stream, typeof(T), default)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var streamPayload = new MemoryStream();
        this._serializer.Serialize(streamPayload, input, input.GetType(), default);
        streamPayload.Position = 0;
        return streamPayload;
    }

    public MemoryStream ToMemoryStream<T>(T input) where T : notnull
    {
        var streamPayload = new MemoryStream();
        this._serializer.Serialize(streamPayload, input, input.GetType(), default);
        streamPayload.Position = 0;
        return streamPayload;
    }
}