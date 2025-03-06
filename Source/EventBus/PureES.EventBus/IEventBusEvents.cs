namespace PureES.EventBus;

public interface IEventBusEvents
{
    /// <summary>
    /// Invoked before an event is handled
    /// </summary>
    Task BeforeEventHandled(EventEnvelope envelope);

    /// <summary>
    /// Invoked after an event is handled
    /// </summary>
    Task AfterEventHandled(EventEnvelope envelope);
}