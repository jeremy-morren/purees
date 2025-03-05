using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JetBrains.Annotations;

namespace PureES.EventStore.EFCore;

/// <summary>
/// Options for the EFCore event store
/// </summary>
[PublicAPI]
public class EfCoreEventStoreOptions
{
    /// <summary>
    /// Custom options for serializing/deserializing events and metadata
    /// </summary>
    public JsonSerializerOptions JsonOptions { get; set; } = new()
    {
        PropertyNamingPolicy = null,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() }
    };
    
    /// <summary>
    /// Type to deserialize metadata as
    /// </summary>
    public Type MetadataType { get; set; } = typeof(JsonElement);
    
    /// <summary>
    /// Database schema to use, if provider supports schemas
    /// </summary>
    public string? Schema { get; set; }

    internal bool Validate()
    {
        if (JsonOptions == null!)
            throw new Exception("EfCore event store JsonOptions must be set");
        
        if (MetadataType == null!)
            throw new Exception("EfCore event store MetadataType must be set");

        return true;
    }

    internal void PostConfigure()
    {
        JsonOptions.MakeReadOnly();
    }
}