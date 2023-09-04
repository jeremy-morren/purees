namespace PureES.Core;

/// <summary>
/// Invokes all event handlers registered for <typeparamref name="TEvent"/>
/// </summary>
/// <typeparam name="TEvent">The event type</typeparam>
public interface IEventHandler<TEvent>
{
    /// <summary>
    /// Invokes all event handlers registered for <typeparamref name="TEvent"/>
    /// </summary>
    Task Handle(EventEnvelope @event, CancellationToken cancellationToken);
}