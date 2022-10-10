using PureES.Core;

namespace PureES.EventBus;

public interface IEventBus
{
    /// <summary>
    /// Publish an event to registered event handlers
    /// for the given event type
    /// </summary>
    /// <param name="envelope">Event to publish</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="AggregateException">
    /// When <see cref="EventBusOptions.PropagateEventHandlerExceptions"/> is true,
    /// all exceptions thrown by event handlers
    /// </exception>
    Task Publish(EventEnvelope envelope, CancellationToken ct);
}
