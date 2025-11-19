// ReSharper disable InconsistentNaming

namespace PureES;

/// <inheritdoc />
public class EventEnvelope : IEventEnvelope
{
    public EventEnvelope(
        string streamId,
        uint streamPosition, 
        DateTime timestamp,
        object _event,
        object? _metadata)
    {
        if (timestamp.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be in UTC", nameof(timestamp));

        StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
        StreamPosition = streamPosition;
        Timestamp = timestamp;
        Event = _event ?? throw new ArgumentNullException(nameof(_event));
        Metadata = _metadata;
    }

    public EventEnvelope(IEventEnvelope other)
    {
        StreamId = other.StreamId;
        StreamPosition = other.StreamPosition;
        Timestamp = other.Timestamp;
        Event = other.Event;
        Metadata = other.Metadata;
    }

    /// <inheritdoc />
    public string StreamId { get; }

    /// <inheritdoc />
    public uint StreamPosition { get; }

    /// <inheritdoc />
    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public object Event { get; }

    /// <inheritdoc />
    public object? Metadata { get; }

    /// <inheritdoc />
    public IEventEnvelope<TEvent, TMetadata> Cast<TEvent, TMetadata>() where TEvent : notnull => Cast<TEvent, TMetadata>(this);

    internal static IEventEnvelope<TEvent, TMetadata> Cast<TEvent, TMetadata>(IEventEnvelope envelope)
        where TEvent : notnull
    {
        if (envelope.Event is not TEvent)
        {
            throw new ArgumentException($"Could not convert {envelope.Event.GetType()} to {typeof(TEvent)}");
        }
        if (envelope.Metadata is not TMetadata)
        {
            var type = envelope.Metadata?.GetType().ToString() ?? "null";
            throw new ArgumentException($"Could not convert {envelope.Event.GetType()} to {type}");
        }
        return new EventEnvelope<TEvent, TMetadata>(envelope);
    }

    /// <summary>
    /// Creates a new <see cref="EventEnvelope"/> with the specified event
    /// </summary>
    public static EventEnvelope WithEvent(IEventEnvelope source, object @event) =>
        new(
            source.StreamId,
            source.StreamPosition,
            source.Timestamp,
            @event,
            source.Metadata
        );

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
    public EventEnvelope(
        string streamId,
        uint streamPosition,
        DateTime timestamp,
        TEvent @event,
        object? metadata)
    {
        if (timestamp.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be in UTC", nameof(timestamp));

        StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
        StreamPosition = streamPosition;
        Timestamp = timestamp;
        Event = @event ?? throw new ArgumentNullException(nameof(@event));

        if (metadata is not TMetadata m)
        {
            var type = metadata?.GetType().ToString() ?? "null";
            throw new ArgumentException($"Could not convert {type} to {typeof(TMetadata)}");
        }

        Metadata = m;
    }
    
    public EventEnvelope(IEventEnvelope source)
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

    public EventEnvelope(IEventEnvelope<TEvent, TMetadata> source)
    {
        StreamId = source.StreamId;
        StreamPosition = source.StreamPosition;
        Timestamp = source.Timestamp;
        Event = source.Event;
        Metadata = source.Metadata;
    }

    /// <inheritdoc />
    public string StreamId { get; }

    /// <inheritdoc />
    public uint StreamPosition { get; }

    /// <inheritdoc />
    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public TEvent Event { get; }

    /// <inheritdoc />
    public TMetadata Metadata { get; }

    /// <inheritdoc />
    public IEventEnvelope<TOther, TMetadata> Cast<TOther>() where TOther : notnull => EventEnvelope.Cast<TOther, TMetadata>(this);

    /// <inheritdoc />
    public IEventEnvelope<TEvent1, TMetadata1> Cast<TEvent1, TMetadata1>() where TEvent1 : notnull => EventEnvelope.Cast<TEvent1, TMetadata1>(this);

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