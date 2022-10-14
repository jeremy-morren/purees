// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable ConditionIsAlwaysTrueOrFalse

using PureES.Core.EventStore;

namespace PureES.Core;

/// <summary>
/// Represents an event persisted to <see cref="IEventStore"/>
/// </summary>
/// <param name="EventId">The unique <see cref="Guid"/> of this event</param>
/// <param name="StreamId">The id of the stream that the event belongs to</param>
/// <param name="StreamPosition">The position of the event within the stream</param>
/// <param name="OverallPosition">
/// The overall position of this event among all events. 
/// Not guaranteed to be contiguous (i.e. 0-1-2....)
/// </param>
/// <param name="Timestamp">The UTC timestamp that the event was persisted</param>
/// <param name="Event">The underlying event</param>
/// <param name="Metadata">The metadata pertaining to the event</param>
public record EventEnvelope(Guid EventId,
    string StreamId,
    ulong StreamPosition,
    ulong OverallPosition,
    DateTime Timestamp,
    object Event,
    object? Metadata)
{
    public bool Equals<TEvent, TMetadata>(EventEnvelope<TEvent, TMetadata>? other)
        where TEvent : notnull
        where TMetadata : notnull
    {
        if (ReferenceEquals(other, null)) return false;
        return EventId.Equals(other.EventId)
               && StreamId == other.StreamId
               && StreamPosition == other.StreamPosition
               && OverallPosition == other.OverallPosition
               && Timestamp.Equals(other.Timestamp)
               && other.Event.Equals(Event)
               && MetadataEquals(other.Metadata);
    }

    private bool MetadataEquals<TMetadata>(TMetadata other) =>
        ReferenceEquals(Metadata, other) 
        || ReferenceEquals(Metadata, null) 
        || ReferenceEquals(other, null)
        || other.Equals(Metadata);
}

public record EventEnvelope<TEvent, TMetadata>
    where TEvent : notnull
{
    /// <summary>
    /// The unique <see cref="Guid"/> of this event
    /// </summary>
    public Guid EventId { get; }
    
    /// <summary>
    /// The id of the stream that the event belongs to
    /// </summary>
    public string StreamId { get; }
    
    /// <summary>
    /// The position of the event within the stream
    /// </summary>
    public ulong StreamPosition { get; }

    /// <summary>
    /// The overall position of this event among all events.
    /// Not guaranteed to be contiguous (i.e. 0-1-2....)
    /// </summary>
    public ulong OverallPosition { get; }

    /// <summary>
    /// The UTC timestamp that the event was persisted
    /// </summary>
    public DateTime Timestamp { get; }
    
    /// <summary>
    /// The underlying event
    /// </summary>
    public TEvent Event { get; }
    
    /// <summary>
    /// The metadata pertaining to the event
    /// </summary>
    public TMetadata Metadata { get; }

    public EventEnvelope(EventEnvelope source)
    {
        EventId = source.EventId;
        StreamId = source.StreamId;
        StreamPosition = source.StreamPosition;
        OverallPosition = source.OverallPosition;
        Timestamp = source.Timestamp;
        if (source.Event == null)
            throw new ArgumentException($"{nameof(source.Event)} is null");
        if (source.Event is not TEvent e)
            throw new ArgumentException($"Could not convert {source.Event.GetType()} to {typeof(TEvent)}");
        Event = e;
        if (source.Metadata == null)
            throw new ArgumentException($"{nameof(source.Metadata)} is null");
        if (source.Metadata is not TMetadata m)
            throw new ArgumentException($"Could not convert {source.Metadata.GetType()} to {typeof(TMetadata)}");
        Metadata = m;
    }
    
    public EventEnvelope(EventEnvelope<TEvent, TMetadata> source)
    {
        EventId = source.EventId;
        StreamId = source.StreamId;
        StreamPosition = source.StreamPosition;
        OverallPosition = source.OverallPosition;
        Timestamp = source.Timestamp;
        Event = source.Event;
        Metadata = source.Metadata;
    }

    public bool Equals(EventEnvelope? other)
    {
        if (ReferenceEquals(other, null)) return false;
        return EventId.Equals(other.EventId)
               && StreamId == other.StreamId
               && StreamPosition == other.StreamPosition
               && OverallPosition == other.OverallPosition
               && Timestamp.Equals(other.Timestamp)
               && Event.Equals(other.Event)
               && MetadataEquals(other.Metadata);
    }

    private bool MetadataEquals(object? other) =>
        ReferenceEquals(Metadata, other) 
        || ReferenceEquals(Metadata, null) 
        || ReferenceEquals(other, null)
        || Metadata.Equals(other);

    public static bool operator ==(EventEnvelope<TEvent, TMetadata>? left, EventEnvelope? right) 
        => left?.Equals(right) ?? ReferenceEquals(right, null);

    public static bool operator !=(EventEnvelope<TEvent, TMetadata>? left, EventEnvelope? right) 
        => !(left == right);
}