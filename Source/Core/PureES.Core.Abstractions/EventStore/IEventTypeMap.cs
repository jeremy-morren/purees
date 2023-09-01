// ReSharper disable InconsistentNaming

namespace PureES.Core.EventStore;

[PublicAPI]
public interface IEventTypeMap
{
    /// <summary>
    ///     Gets a string representing <paramref name="eventType" />
    ///     for persistence
    /// </summary>
    /// <param name="eventType">The event type</param>
    /// <returns></returns>
    string GetTypeName(Type eventType);

    /// <summary>
    ///     Gets a CLR type from the provided <paramref name="typeName" />
    /// </summary>
    /// <param name="typeName">A string representing a CLR type</param>
    /// <returns></returns>
   Type GetCLRType(string typeName);
}