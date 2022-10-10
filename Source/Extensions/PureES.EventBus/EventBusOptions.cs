namespace PureES.EventBus;

public class EventBusOptions
{
    /// <summary>
    /// Indicates whether exceptions inside event handlers
    /// should be rethrown (defaults to <c>false</c>)
    /// </summary>
    /// <remarks>
    /// When true, this will manifest as an <see cref="AggregateException"/>
    /// on <see cref="IEventBus.Publish"/>
    /// </remarks>
    public bool PropagateEventHandlerExceptions { get; set; } = false;
}