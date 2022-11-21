using System.Text.Json;

namespace PureES.Core.EventStore.Serialization;

/// <summary>
///     An implementation of <see cref="IEventStoreSerializer" />
///     using <c>System.Text.Json</c> serializer
/// </summary>
public class JsonEventStoreSerializer : IEventStoreSerializer
{
    private const string ContentType = "application/json";
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonEventStoreSerializer(JsonSerializerOptions jsonOptions) => _jsonOptions = jsonOptions;

    public object? Deserialize(ReadOnlySpan<byte> data, Type? objectType) =>
        JsonSerializer.Deserialize(data, objectType ?? typeof(JsonElement), _jsonOptions);

    public byte[] Serialize(object value, out string contentType)
    {
        contentType = ContentType;
        return JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
    }
}