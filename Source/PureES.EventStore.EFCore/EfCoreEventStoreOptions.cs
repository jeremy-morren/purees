using System.Text.Json;
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
    public JsonSerializerOptions JsonOptions { get; set; } = new();
    
    /// <summary>
    /// Type to deserialize metadata as
    /// </summary>
    public Type MetadataType { get; set; } = typeof(JsonElement);

    internal bool Validate()
    {
        if (JsonOptions == null!)
            throw new Exception("EfCore event store JsonOptions must be set");
        
        if (MetadataType == null!)
            throw new Exception("EfCore event store MetadataType must be set");

        return true;
    }
}