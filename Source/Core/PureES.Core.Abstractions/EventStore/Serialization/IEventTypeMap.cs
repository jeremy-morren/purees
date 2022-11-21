using System.Diagnostics.CodeAnalysis;

namespace PureES.Core.EventStore.Serialization;

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
    ///     Gets the CLR type from <paramref name="typeName" />
    /// </summary>
    /// <param name="typeName">A string representing a CLR type</param>
    /// <param name="clrType">The matched CLR type, if resolved</param>
    /// <returns></returns>
    bool TryGetType(string typeName, [MaybeNullWhen(false)] out Type clrType);
}