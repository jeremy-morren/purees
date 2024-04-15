using System.Text.Json;
using JetBrains.Annotations;

namespace PureES.EventStores.Marten;

[PublicAPI]
public class MartenEventStoreOptions
{
    /// <summary>
    /// The database schema used for storing events
    /// </summary>
    /// <remarks>
    /// Default is <c>purees</c>
    /// </remarks>
    public string DatabaseSchema { get; set; } = "purees";

    /// <summary>
    /// The type to deserialize metadata to
    /// </summary>
    public Type MetadataType { get; set; } = typeof(JsonElement?);

    /// <summary>
    /// Json options for serializing/deserializing events/metadata
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool Validate()
    {
        Require(nameof(DatabaseSchema), DatabaseSchema);
        Require(nameof(MetadataType), MetadataType);
        Require(nameof(JsonSerializerOptions), JsonSerializerOptions);
        return true;
    }
    
    private static void Require(string name, object value)
    {
        if ((value is string str && string.IsNullOrWhiteSpace(str)) || value == null)
            throw new Exception($"{name} is required");
    }
}