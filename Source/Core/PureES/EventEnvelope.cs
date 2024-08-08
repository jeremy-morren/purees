

// ReSharper disable InconsistentNaming

namespace PureES;

/// <summary>
///     Represents an event persisted to <see cref="IEventStore" />
/// </summary>
public class EventEnvelope : IEventEnvelope
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
    
    internal static bool Equal(IEventEnvelope? left, IEventEnvelope? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (ReferenceEquals(left, null)) return false;
        if (ReferenceEquals(right, null)) return false;

        return left.StreamId == right.StreamId &&
               left.StreamPosition == right.StreamPosition &&
               left.Timestamp == right.Timestamp &&
               MetadataEquals(left.Metadata, right.Metadata) &&
               left.Event.Equals(right.Event);
    }
    
    private static bool MetadataEquals(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (ReferenceEquals(left, null)) return false;
        if (ReferenceEquals(right, null)) return false;
        return left.Equals(right);
    }

    public bool Equals(IEventEnvelope? other) => Equal(this, other);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is EventEnvelope e && Equals(e);
    }

    public override int GetHashCode() => 
        HashCode.Combine(Event, Metadata, StreamId, StreamPosition, Timestamp);

    public static bool operator ==(EventEnvelope? left, EventEnvelope? right)
        => left?.Equals(right) ?? ReferenceEquals(right, null);

    public static bool operator !=(EventEnvelope? left, EventEnvelope? right)
        => !(left == right);

    #endregion
}

/// <summary>
/// Represents a strongly-typed event persisted to <see cref="IEventStore" />
/// </summary>
public class EventEnvelope<TEvent, TMetadata> : IEventEnvelope<TEvent, TMetadata>, IEquatable<IEventEnvelope<TEvent, TMetadata>> 
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
        StreamId = source.StreamId;
        StreamPosition = source.StreamPosition;
        Timestamp = source.Timestamp;
        Event = source.Event;
        Metadata = source.Metadata;
    }

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
    
    object IEventEnvelope.Event => Event;
    
    object? IEventEnvelope.Metadata => Metadata;

    public override string? ToString() => new 
    {
        StreamId,
        StreamPosition,
        Timestamp,
        Event,
        Metadata
    }.ToString();
    
    #region Equality
    
    private static bool MetadataEquals(TMetadata? left, TMetadata? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (ReferenceEquals(left, null)) return false;
        if (ReferenceEquals(right, null)) return false;
        return left.Equals(right);
    }
    
    internal static bool Equal(IEventEnvelope<TEvent, TMetadata>? left, IEventEnvelope<TEvent, TMetadata>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (ReferenceEquals(left, null)) return false;
        if (ReferenceEquals(right, null)) return false;

        return left.StreamId == right.StreamId &&
               left.StreamPosition == right.StreamPosition &&
               left.Timestamp == right.Timestamp &&
               MetadataEquals(left.Metadata, right.Metadata) &&
               left.Event.Equals(right.Event);
    }
    
    public bool Equals(IEventEnvelope? other)
    {
        return EventEnvelope.Equal(this, other);
    }
    
    public bool Equals(IEventEnvelope<TEvent, TMetadata>? other) => Equal(this, other);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is IEventEnvelope<TEvent, TMetadata> e && Equals(e);
    }

    public override int GetHashCode() => 
        HashCode.Combine(Event, Metadata, StreamId, StreamPosition, Timestamp);

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