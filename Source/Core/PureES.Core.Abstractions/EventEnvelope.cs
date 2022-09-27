// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable ConditionIsAlwaysTrueOrFalse

namespace PureES.Core;

public record EventEnvelope(Guid EventId,
    string StreamId,
    ulong StreamPosition,
    DateTime Timestamp,
    object Event,
    object? Metadata
)
{
    public bool Equals<TEvent, TMetadata>(EventEnvelope<TEvent, TMetadata>? other)
        where TEvent : notnull
        where TMetadata : notnull
    {
        if (ReferenceEquals(other, null)) return false;
        return EventId.Equals(other.EventId)
               && StreamId == other.StreamId
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
}

public record EventEnvelope<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    public Guid EventId { get; }
    public string StreamId { get; }
    public ulong StreamPosition { get; }
    public DateTime Timestamp { get; }
    public TEvent Event { get; }
    public TMetadata Metadata { get; }

    public EventEnvelope(EventEnvelope source)
    {
        EventId = source.EventId;
        StreamId = source.StreamId;
        StreamPosition = source.StreamPosition;
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