namespace PureES.Core.EventStore;

/// <summary>
///     A repository wrapping an <c>EventSourcing</c> database
/// </summary>
/// <remarks>
///     Any <c>Version</c> parameter is 0-index based
/// </remarks>
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
    /// <returns>The revision (0-index based) revision of <paramref name="streamId" /></returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    public Task<ulong> GetRevision(string streamId, CancellationToken cancellationToken);
    
    /// <summary>
    ///     Gets the current revision that stream <paramref name="streamId" /> is at
    /// </summary>
    /// <param name="streamId">Event stream to query</param>
    /// <param name="expectedRevision">The expected revision of <paramref name="streamId"/></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The revision (0-index based) revision of <paramref name="streamId" /></returns>
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
    public Task<ulong> Create(string streamId,
        UncommittedEvent @event,
        CancellationToken cancellationToken);

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
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from all events in the order in which they were added
    /// </returns>
    public IAsyncEnumerable<EventEnvelope> ReadAll(CancellationToken cancellationToken);

    /// <summary>
    ///     Reads events from stream <paramref name="streamId" />
    /// </summary>
    /// <param name="streamId">Id of stream to load events from</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from the events in the stream in the order in which they were added
    /// </returns>
    /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
    public IAsyncEnumerable<EventEnvelope> Read(string streamId, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads events from stream <paramref name="streamId" />,
    ///     ensuring stream is at <see cref="expectedRevision" />
    /// </summary>
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
    public IAsyncEnumerable<EventEnvelope> Read(string streamId,
        ulong expectedRevision,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Loads events from stream <paramref name="streamId" /> up
    ///     to and including <paramref name="requiredRevision" />
    /// </summary>
    /// <param name="streamId">Id of stream to load events from</param>
    /// <param name="requiredRevision">Minimum revision that stream must be at</param>
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
    public IAsyncEnumerable<EventEnvelope> ReadPartial(string streamId,
        ulong requiredRevision,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Loads events by <c>EventType</c>
    /// </summary>
    /// <param name="eventType">Type of event to query</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
    ///     from the events in the stream in the order in which they were added
    /// </returns>
    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Type eventType, CancellationToken cancellationToken);

    /// <summary>
    /// Reads multiple streams, returning events in chronological order
    /// </summary>
    /// <param name="streams">The streams to read</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A combined stream of events, in chronological order</returns>
    public IAsyncEnumerable<EventEnvelope> ReadMany(IEnumerable<string> streams, CancellationToken cancellationToken);
    
    /// <summary>
    /// Reads multiple streams, returning events in chronological order
    /// </summary>
    /// <param name="streams">The streams to read</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A combined stream of events, in chronological order</returns>
    public IAsyncEnumerable<EventEnvelope> ReadMany(IAsyncEnumerable<string> streams, CancellationToken cancellationToken);
}