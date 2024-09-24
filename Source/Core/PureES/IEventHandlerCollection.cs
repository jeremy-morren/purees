namespace PureES;

/// <summary>
/// Do not use directly, use <see cref="IEventHandlerCollection{TEvent}"/> instead
/// </summary>
[PublicAPI]
public interface IEventHandlerCollection : IReadOnlyList<IEventHandler>
{
    /// <summary>
    /// Gets all event handlers that can handle the given event.
    /// </summary>
    /// <param name="event">The event to filter</param>
    /// <returns></returns>
    IEnumerable<IEventHandler> GetHandlers(EventEnvelope @event);
}

/// <summary>
/// All event handlers registered for a given event type, ordered by priority.
/// </summary>
[PublicAPI]
public interface IEventHandlerCollection<TEvent> : IEventHandlerCollection;