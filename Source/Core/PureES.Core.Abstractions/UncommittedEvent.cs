﻿using PureES.Core.EventStore;

namespace PureES.Core;

/// <summary>
///     Represents an event that has not been persisted to <see cref="IEventStore" />
/// </summary>
[PublicAPI]
public sealed record UncommittedEvent
{
    /// <summary>
    /// The <see cref="Guid" /> representing this event
    /// </summary>
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The event belonging to this record
    /// </summary>
    public required object Event { get; init; }

    /// <summary>
    /// The event metadata belonging to this record
    /// </summary>
    public object? Metadata { get; set; }
}