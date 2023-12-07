

// ReSharper disable InconsistentNaming

namespace PureES;

/// <summary>
///     Represents an event persisted to <see cref="IEventStore" />
/// </summary>

[PublicAPI]
public class EventEnvelope : IEquatable<EventEnvelope>
{
    public EventEnvelope(EventEnvelope other)
    {
        Event = other.Event;
        Metadata = other.Metadata;
        StreamId = other.StreamId;
        StreamPosition = other.StreamPosition;
        Timestamp = other.Timestamp;
    }

    public EventEnvelope(string streamId, 
        ulong streamPosition, 
        DateTime timestamp,
        object @event,
        object? metadata)
    {
        Event = @event ?? throw new ArgumentNullException(nameof(@event));
        Metadata = metadata;
        StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
        StreamPosition = streamPosition;
        Timestamp = timestamp;
        if (timestamp.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be in UTC", nameof(timestamp));
    }

    /// <summary>The id of the stream that the event belongs to</summary>
    public string StreamId { get; }

    /// <summary>The position of the event within the stream</summary>
    public ulong StreamPosition { get; }

    /// <summary>The UTC timestamp that the event was persisted</summary>
    public DateTime Timestamp { get; }

    /// <summary>The underlying event</summary>
    public object Event { get; }

    /// <summary>The metadata pertaining to the event</summary>
    public object? Metadata { get; }

    public override string? ToString() => new
    {
        StreamId,
        StreamPosition,
        Timestamp,
        Event,
        Metadata
    }.ToString();

    #region Equality
    
    public bool Equals(EventEnvelope? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Event.Equals(other.Event) &&
               MetadataEquals(other.Metadata) &&
               StreamId == other.StreamId &&
               StreamPosition == other.StreamPosition &&
               Timestamp.Equals(other.Timestamp);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is EventEnvelope e && Equals(e);
    }

    public override int GetHashCode() => 
        HashCode.Combine(Event, Metadata, StreamId, StreamPosition, Timestamp);
    
    public bool Equals<TEvent, TMetadata>(EventEnvelope<TEvent, TMetadata>? other)
        where TEvent : notnull
        where TMetadata : notnull
    {
        if (ReferenceEquals(other, null)) return false;
        return StreamId == other.StreamId
               && StreamPosition == other.StreamPosition
               && Timestamp.Equals(other.Timestamp)
               && other.Event.Equals(Event)
               && MetadataEquals(other.Metadata);
    }

    private bool MetadataEquals<TMetadata>(TMetadata other) =>
        ReferenceEquals(Metadata, other)
        || ReferenceEquals(Metadata, null)
        || ReferenceEquals(other, null)
        || other.Equals(Metadata);

    public static bool operator ==(EventEnvelope? left, EventEnvelope? right)
        => left?.Equals(right) ?? ReferenceEquals(right, null);

    public static bool operator !=(EventEnvelope? left, EventEnvelope? right)
        => !(left == right);

    #endregion
}

[PublicAPI]
public class EventEnvelope<TEvent, TMetadata> : IEquatable<EventEnvelope<TEvent, TMetadata>>, IEquatable<EventEnvelope>
    where TEvent : notnull
{
    public EventEnvelope(EventEnvelope source)
    {
        StreamId = source.StreamId;
        StreamPosition = source.StreamPosition;
        Timestamp = source.Timestamp;
        
        if (source.Event == null)
            throw new ArgumentException($"{nameof(source.Event)} is null");
        if (source.Event is not TEvent e)
            throw new ArgumentException($"Could not convert {source.Event.GetType()} to {typeof(TEvent)}");
        Event = e;
        
        if (source.Metadata is not TMetadata m)
        {
            var type = source.Metadata?.GetType().ToString() ?? "null";
            throw new ArgumentException($"Could not convert {type} to {typeof(TMetadata)}");
        }

        Metadata = m;
    }

    public EventEnvelope(EventEnvelope<TEvent, TMetadata> source)
    {
        EventId = source.EventId;
        StreamId = source.StreamId;
        StreamPosition = source.StreamPosition;
        Timestamp = source.Timestamp;
        Event = source.Event;
        Metadata = source.Metadata;
    }

    /// <summary>
    ///     The unique <see cref="Guid" /> of this event
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    ///     The id of the stream that the event belongs to
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    ///     The position of the event within the stream
    /// </summary>
    public ulong StreamPosition { get; }

    /// <summary>
    ///     The UTC timestamp that the event was persisted
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    ///     The underlying event
    /// </summary>
    public TEvent Event { get; }

    /// <summary>
    ///     The metadata pertaining to the event
    /// </summary>
    public TMetadata Metadata { get; }

    public override string? ToString() => new 
    {
        EventId,
        StreamId,
        StreamPosition,
        Timestamp,
        Event,
        Metadata
    }.ToString();
    
    #region Equality
    
    public bool Equals(EventEnvelope<TEvent, TMetadata>? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Event.Equals(other.Event) &&
               MetadataEquals(other.Metadata) &&
               EventId.Equals(other.EventId) &&
               StreamId == other.StreamId &&
               StreamPosition == other.StreamPosition &&
               Timestamp.Equals(other.Timestamp);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((EventEnvelope<TEvent, TMetadata>) obj);
    }

    public override int GetHashCode() => 
        HashCode.Combine(Event, Metadata, EventId, StreamId, StreamPosition, Timestamp);
    
    public bool Equals(EventEnvelope? other)
    {
        if (ReferenceEquals(other, null)) return false;
        return StreamId == other.StreamId
               && StreamPosition == other.StreamPosition
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
    
    public static bool operator ==(EventEnvelope? left, EventEnvelope<TEvent, TMetadata>? right)
        => right?.Equals(left) ?? ReferenceEquals(left, null);

    public static bool operator !=(EventEnvelope? left, EventEnvelope<TEvent, TMetadata>? right)
        => !(right == left);

    #endregion

}