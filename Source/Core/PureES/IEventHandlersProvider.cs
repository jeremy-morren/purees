namespace PureES;

/// <summary>
/// Retrieves all event handlers for a given event type.
/// </summary>
[PublicAPI]
public interface IEventHandlersProvider
{
    /// <summary>
    /// Retrieves all event handlers for a given event type.
    /// </summary>
    IEventHandlerCollection GetHandlers(Type eventType);
}