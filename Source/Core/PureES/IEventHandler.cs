using System.Reflection;

namespace PureES;

/// <summary>
/// Base interface for <see cref="IEventHandler{TEvent}"/>
/// </summary>
[PublicAPI]
public interface IEventHandler
{
    /// <summary>
    /// Invokes all registered event handlers for <c>TEvent</c> from <see cref="IEventHandler{TEvent}"/>
    /// </summary>
    Task Handle(EventEnvelope @event);

    /// <summary>
    /// Gets the method that handles the event
    /// </summary>
    MethodInfo Method { get; }

    /// <summary>
    /// The event handler priority (lower priority is executed first)
    /// </summary>
    int Priority { get; }
}

/// <summary>
/// Invokes all event handlers registered for <typeparamref name="TEvent"/>
/// </summary>
/// <typeparam name="TEvent">The event type</typeparam>
[PublicAPI]
public interface IEventHandler<TEvent> : IEventHandler;