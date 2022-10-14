namespace PureES.Core.EventStore;

/// <summary>
/// Represents an event that has not been persisted to <see cref="IEventStore"/>
/// </summary>
public sealed record UncommittedEvent(Guid EventId, object Event, object? Metadata)
{
    /// <summary>
    /// The <see cref="Guid" /> representing this event.
    /// </summary>
    public readonly Guid EventId = EventId;
    
    /// <summary>
    /// The event belonging to this record
    /// </summary>
    public readonly object Event = Event;
    
    /// <summary>
    /// The event metadata belonging to this record
    /// </summary>
    public readonly object? Metadata = Metadata;
}