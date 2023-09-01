﻿using JetBrains.Annotations;

namespace PureES.Core.EventStore;

/// <summary>
///     A repository wrapping an <c>EventSourcing</c> database
/// </summary>
/// <remarks>
///     Any <c>Version</c> parameter is 0-index based
/// </remarks>
[PublicAPI]
public interface IEventStore
{
    /// <summary>
    ///     Checks whether a stream with id <paramref name="streamId" /> exists
    /// </summary>
    /// <param name="streamId">Event stream to query</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<bool> Exists(string streamId, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the current revision that stream <paramref name="streamId" /> is at
    /// </summary>
    /// <param name="streamId">Event stream to query</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The revision (EventStoreReadDirection direction, 0-index based) revision of <paramref name="streamId" /></returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    public Task<ulong> GetRevision(string streamId, CancellationToken cancellationToken);
    
    /// <summary>
    ///     Gets the current revision that stream <paramref name="streamId" /> is at
    /// </summary>
    /// <param name="streamId">Event stream to query</param>
    /// <param name="expectedRevision">The expected revision of <paramref name="streamId"/></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The revision (EventStoreReadDirection direction, 0-index based) revision of <paramref name="streamId" /></returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    /// <remarks>This method is chiefly for optimistic concurrency</remarks>
    public Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken cancellationToken);

    /// <summary>
    ///     Creates a new stream with id <paramref name="streamId" /> using <paramref name="events" />
    /// </summary>
    /// <param name="streamId">Event stream to create</param>
    /// <param name="events">Events to append</param>
    /// <param name="cancellationToken"></param>
    /// <returns><c>Revision</c> of created stream</returns>
    /// <exception cref="StreamAlreadyExistsException">Stream <paramref name="streamId" /> already exists</exception>
    public Task<ulong> Create(string streamId,
        IEnumerable<UncommittedEvent> events,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Creates a new stream with id <paramref name="streamId" /> using <paramref name="event" />
    /// </summary>
    /// <param name="streamId">Event stream to create</param>
    /// <param name="event">Event to append</param>
    /// <param name="cancellationToken"></param>
    /// <returns><c>Revision</c> of created stream</returns>
    /// <exception cref="StreamAlreadyExistsException">Stream <paramref name="streamId" /> already exists</exception>
    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken);

    /// <summary>
    ///     Appends <paramref name="events" /> to stream <paramref name="streamId" />
    /// </summary>
    /// <param name="streamId">Event stream to append to</param>
    /// <param name="expectedRevision">Current stream revision</param>
    /// <param name="events">Events to append to <paramref name="streamId" /></param>
    /// <param name="cancellationToken"></param>
    /// <returns><c>Revision</c> of stream after append</returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    public Task<ulong> Append(string streamId,
        ulong expectedRevision,
        IEnumerable<UncommittedEvent> events,
        CancellationToken cancellationToken);
    
    /// <summary>
    ///     Appends <paramref name="events" /> to stream <paramref name="streamId" />
    /// </summary>
    /// <param name="streamId">Event stream to append to</param>
    /// <param name="events">Events to append to <paramref name="streamId" /></param>
    /// <param name="cancellationToken"></param>
    /// <returns><c>Revision</c> of stream after append</returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    public Task<ulong> Append(string streamId,
        IEnumerable<UncommittedEvent> events,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Appends <paramref name="event" /> to stream <paramref name="streamId" />
    /// </summary>
    /// <param name="streamId">Event stream to append to</param>
    /// <param name="expectedRevision">Current stream revision</param>
    /// <param name="event">Event to append to <paramref name="streamId" /></param>
    /// <param name="cancellationToken"></param>
    /// <returns><c>Revision</c> of stream after append</returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    public Task<ulong> Append(string streamId,
        ulong expectedRevision,
        UncommittedEvent @event,
        CancellationToken cancellationToken);
    
    /// <summary>
    ///     Appends <paramref name="event" /> to stream <paramref name="streamId" />
    /// </summary>
    /// <param name="streamId">Event stream to append to</param>
    /// <param name="event">Event to append to <paramref name="streamId" /></param>
    /// <param name="cancellationToken"></param>
    /// <returns><c>Revision</c> of stream after append</returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    public Task<ulong> Append(string streamId,
        UncommittedEvent @event,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Reads all events in chronological order
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from all events in the order in which they were added
    /// </returns>
    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads all events in chronological order
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="maxCount">The maximum number of events to return</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from all events in the order in which they were added
    /// </returns>
    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction,
        ulong maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads events from stream <paramref name="streamId" />
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="streamId">Id of stream to load events from</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from the events in the stream in the order in which they were added
    /// </returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, 
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads events from stream <paramref name="streamId" />,
    ///     ensuring stream is at <see cref="expectedRevision" />
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="streamId">Id of stream to load events from</param>
    /// <param name="expectedRevision"><c>Revision</c> that the stream is expected to be at</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from the events in the stream in the order in which they were added
    /// </returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    /// <exception cref="WrongStreamRevisionException">
    ///     Stream <paramref name="streamId" /> not at revision <paramref name="expectedRevision" />
    /// </exception>
    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, 
        string streamId,
        ulong expectedRevision,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads events from stream <paramref name="streamId" /> up
    ///     to and including <paramref name="requiredRevision" />
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="streamId">Id of stream to load events from</param>
    /// <param name="requiredRevision">Minimum revision that stream must be at (relative to <paramref name="direction"/>)</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from the events in the stream in the order in which they were added,
    ///     up to and including event at <paramref name="requiredRevision" />
    /// </returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    /// <exception cref="WrongStreamRevisionException">
    ///     Revision of stream <paramref name="streamId" /> less than <paramref name="requiredRevision" />
    /// </exception>
    public IAsyncEnumerable<EventEnvelope> ReadPartial(Direction direction,
        string streamId,
        ulong requiredRevision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads multiple streams, returning events in chronological order
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="streams">The streams to read</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A combined stream of events, in chronological order</returns>
    public IAsyncEnumerable<EventEnvelope> ReadMany(Direction direction,
        IEnumerable<string> streams, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reads multiple streams, returning events in chronological order
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="streams">The streams to read</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A combined stream of events, in chronological order</returns>
    public IAsyncEnumerable<EventEnvelope> ReadMany(Direction direction, 
        IAsyncEnumerable<string> streams, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reads multiple streams. Order is undefined
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="streams">The streams to read</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A combined stream of events, in chronological order</returns>
    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMultiple(Direction direction,
        IEnumerable<string> streams, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reads multiple streams. Order is undefined
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="streams">The streams to read</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A combined stream of events, in chronological order</returns>
    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMultiple(Direction direction, 
        IAsyncEnumerable<string> streams, 
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads events by <c>EventType</c>
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="eventType">Type of event to query</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from the events in the stream in the order in which they were added
    /// </returns>
    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction,
        Type eventType, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    ///     Loads events by <c>EventType</c>
    /// </summary>
    /// <param name="direction">Read direction</param>
    /// <param name="eventType">Type of event to query</param>
    /// <param name="maxCount">The maximum number of events to return</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from the events in the stream in the order in which they were added
    /// </returns>
    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction,
        Type eventType, 
        ulong maxCount,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    ///     Gets the total number of events
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///    The total number of events in the event store.
    /// </returns>
    public Task<ulong> Count(CancellationToken cancellationToken);
    
    /// <summary>
    ///     Counts events by <c>EventType</c>
    /// </summary>
    /// <param name="eventType">Type of event to query</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from the events in the stream in the order in which they were added
    /// </returns>
    public Task<ulong> CountByEventType(Type eventType, CancellationToken cancellationToken);
}