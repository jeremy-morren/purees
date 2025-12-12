// ReSharper disable InconsistentNaming

using System.Collections.Immutable;

namespace PureES;

[PublicAPI]
public interface IEventTypeMap
{
    /// <summary>
    ///     Gets a string array representing the full inheritance hierarchy of
    ///     <paramref name="eventType" /> for persistence
    /// </summary>
    /// <param name="eventType">The event type</param>
    /// <returns>Type names with full hierarchy (base type first, actual type last)</returns>
    ImmutableArray<string> GetTypeNames(Type eventType);

    /// <summary>
    ///     Gets a CLR type from the provided <paramref name="typeName" />
    /// </summary>
    /// <param name="typeName">A string representing a CLR type</param>
    /// <returns></returns>
    Type GetCLRType(string typeName);
}