namespace PureES;

/// <summary>
/// Represents an event that has not been persisted to <see cref="IEventStore" />
/// </summary>
[PublicAPI]
public class UncommittedEvent
{
    public UncommittedEvent(object @event)
    {
        Event = @event ?? throw new ArgumentNullException(nameof(@event));
    }

    /// <summary>
    /// The event belonging to this record
    /// </summary>
    public object Event { get; }

    /// <summary>
    /// The event metadata belonging to this record
    /// </summary>
    public object? Metadata { get; set; }
}