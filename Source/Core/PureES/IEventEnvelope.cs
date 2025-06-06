﻿

// ReSharper disable InconsistentNaming

namespace PureES;

/// <summary>
///     Represents an event persisted to <see cref="IEventStore" />
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors | ImplicitUseTargetFlags.WithMembers)]
public interface IEventEnvelope : IEquatable<IEventEnvelope>
{
    /// <summary>The id of the stream that the event belongs to</summary>
    public string StreamId { get; }

    /// <summary>The position of the event within the stream</summary>
    public uint StreamPosition { get; }

    /// <summary>The UTC timestamp that the event was persisted</summary>
    public DateTime Timestamp { get; }

    /// <summary>The underlying event</summary>
    object Event { get; }

    /// <summary>The metadata pertaining to the event</summary>
    object? Metadata { get; }

    /// <summary>
    /// Casts the event to a specific type
    /// </summary>
    IEventEnvelope<TEvent, TMetadata> Cast<TEvent, TMetadata>()
        where TEvent : notnull;
}

/// <summary>
/// Represents a strongly-typed event persisted to <see cref="IEventStore" />
/// </summary>
/// <remarks>
/// IEquatable is not implemented to enable generic parameters to be covariant
/// </remarks>
public interface IEventEnvelope<out TEvent, out TMetadata> : IEventEnvelope
    where TEvent : notnull
{
    /// <summary>The underlying event</summary>
    new TEvent Event { get; }

    /// <summary>The metadata pertaining to the event</summary>
    new TMetadata Metadata { get; }

    /// <summary>
    /// Casts the event to a specific type
    /// </summary>
    IEventEnvelope<TOther, TMetadata> Cast<TOther>() where TOther : notnull;
}