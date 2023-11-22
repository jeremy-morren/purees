namespace PureES.Core;

/// <summary>
///     Represents an event that has not been persisted to <see cref="IEventStore" />
/// </summary>
[PublicAPI]
public sealed record UncommittedEvent(object Event)
{
    /// <summary>
    /// The event belonging to this record
    /// </summary>
    public object Event { get; init; } = Event ?? throw new ArgumentNullException(nameof(Event));

    /// <summary>
    /// The event metadata belonging to this record
    /// </summary>
    public object? Metadata { get; set; }
}