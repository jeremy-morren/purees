using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PureES.EventStore.InMemory;

public class InMemoryEventStoreOptions
{
    /// <summary>
    /// Gets or sets the JSON serializer options to use
    /// when deserializing events &amp; metadata
    /// </summary>
    public JsonSerializerOptions JsonOptions { get; } = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = null,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    /// <summary>
    /// Gets or sets the type to deserialize metadata as
    /// </summary>
    public Type MetadataType { get; set; } = typeof(JsonElement?);

    internal bool Validate()
    {
        if (MetadataType == null)
            throw new Exception($"{nameof(InMemoryEventStoreOptions)}.{nameof(MetadataType)} is required");
        return true;
    }

    internal void PostConfigure()
    {
        JsonOptions.MakeReadOnly();
    }
}