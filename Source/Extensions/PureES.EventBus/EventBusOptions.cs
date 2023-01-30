namespace PureES.EventBus;

public class EventBusOptions
{
    /// <summary>
    ///     Indicates whether exceptions inside event handlers
    ///     should be rethrown (defaults to <c>false</c>)
    /// </summary>
    /// <remarks>
    ///     When true, this will manifest as an <see cref="AggregateException" />
    ///     on <see cref="IEventBus.Publish" />
    /// </remarks>
    public bool PropagateEventHandlerExceptions { get; set; } = false;

    /// <summary>
    /// Gets or sets the target length in milliseconds for a projection to be handled. 
    /// </summary>
    /// <remarks>
    /// If the handler takes longer, a warning will be logged
    /// </remarks>
    public double TargetLength { get; set; } = 250;

    public void Validate()
    {
        if (TargetLength <= 0)
            throw new Exception("Target length must be greater than 0");
    }
}