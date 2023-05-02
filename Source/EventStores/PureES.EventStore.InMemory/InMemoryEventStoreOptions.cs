using System.Text.Json;

namespace PureES.EventStore.InMemory;

public class InMemoryEventStoreOptions
{
    /// <summary>
    /// Gets or sets the JSON serializer options to use
    /// when deserializing events & metadata
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets or sets the type to deserialize metadata as
    /// </summary>
    public Type MetadataType { get; set; } = typeof(JsonElement?);

    public void Validate()
    {
        if (MetadataType == null)
            throw new Exception("Metadata type is required");
    }
}