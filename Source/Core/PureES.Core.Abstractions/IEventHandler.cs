namespace PureES.Core;

/// <summary>
/// Base interface for <see cref="IEventHandler{TEvent}"/>
/// </summary>
public interface IEventHandler
{
    /// <summary>
    /// Invokes all registered event handlers for <c>TEvent</c> from <see cref="IEventHandler{TEvent}"/>
    /// </summary>
    Task Handle(EventEnvelope @event);
}

/// <summary>
/// Invokes all event handlers registered for <typeparamref name="TEvent"/>
/// </summary>
/// <typeparam name="TEvent">The event type</typeparam>
public interface IEventHandler<TEvent> : IEventHandler
{
}