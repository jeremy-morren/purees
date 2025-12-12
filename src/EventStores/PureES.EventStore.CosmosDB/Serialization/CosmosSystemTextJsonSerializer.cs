using System.Text.Json;

namespace PureES.EventStore.CosmosDB.Serialization;

/// <summary>
/// Uses <see cref="Azure.Core.Serialization.JsonObjectSerializer"/> which leverages System.Text.Json providing a simple API to interact with on the Azure SDKs.
/// </summary>
internal class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions options) => _options = options;

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream is {CanSeek: true, Length: 0})
                return default!;

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
                return (T)(object)stream;
            
            return JsonSerializer.Deserialize<T>(stream, _options)!;
        }
    }

    public override Stream ToStream<T>(T input) => ToMemoryStream(input);

    public MemoryStream ToMemoryStream<T>(T input)
    {
        var ms = new MemoryStream();
        JsonSerializer.Serialize<T>(ms, input, _options);
        ms.Position = 0;
        return ms;
    }
}