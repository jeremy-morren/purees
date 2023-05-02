namespace PureES.Core;

/// <summary>
/// Provides delegates to process events
/// </summary>
public interface IEventHandlersCollection
{
    /// <summary>
    /// Gets all registered event handlers  (via <see cref="EventHandlerAttribute"/>) for an event type
    /// </summary>
    /// <param name="eventType">The event type</param>
    Func<EventEnvelope, IServiceProvider, CancellationToken, Task>[] GetEventHandlers(Type eventType);
}