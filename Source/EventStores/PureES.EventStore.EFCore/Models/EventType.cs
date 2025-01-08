using System.Collections.Immutable;

namespace PureES.EventStore.EFCore.Models;

/// <summary>
/// Separate class for event types to be stored in separate table (one to many)
/// </summary>
internal record EventType
{
    /// <summary>
    /// Serialized type name
    /// </summary>
    public required string TypeName { get; init; }
    
    public static List<EventType> New(ImmutableArray<string> types) =>
        types.Select(t => new EventType { TypeName = t }).ToList();
    
    public static List<EventType> New(Type type, IEventTypeMap map) =>
        New(map.GetTypeNames(type));
}