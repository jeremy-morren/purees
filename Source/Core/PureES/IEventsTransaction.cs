namespace PureES;

/// <summary>
/// A map of multiple event streams to be committed to the database in a single transaction.
/// </summary>
[PublicAPI]
public interface IEventsTransaction : IEnumerable<KeyValuePair<string, EventsList>>
{
    /// <summary>
    /// Adds a new stream to the transaction.
    /// </summary>
    /// <param name="streamId">The event stream id</param>
    /// <param name="revision">
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </param>
    /// <param name="events">Stream events.</param>
    void Add(string streamId, uint? revision, IEnumerable<object> events);

    /// <summary>
    /// Adds a new stream to the transaction.
    /// </summary>
    /// <param name="streamId">The event stream id</param>
    /// <param name="revision">
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </param>
    /// <param name="events">Stream events.</param>
    void Add(string streamId, uint? revision, params object[] events);

    /// <summary>
    /// Adds a new stream to the transaction.
    /// </summary>
    /// <param name="streamId">The event stream id</param>
    /// <param name="revision">
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </param>
    /// <param name="events">Stream events.</param>
    void Add<T>(string streamId, uint? revision, T[] events);

    /// <summary>
    /// Adds a new stream to the transaction.
    /// </summary>
    /// <param name="streamId">The event stream id</param>
    /// <param name="events">Stream events list.</param>
    void Add(string streamId, EventsList events);

    /// <summary>
    /// Adds a new stream to the transaction, or appends events to an existing stream.
    /// </summary>
    /// <param name="streamId">The event stream id</param>
    /// <param name="revision">
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </param>
    /// <param name="events">Stream events.</param>
    void AddOrAppend(string streamId, uint? revision, params object[] events);

    /// <summary>
    /// Gets the events for the specified stream.
    /// </summary>
    /// <param name="streamId"></param>
    EventsList this[string streamId] { get; }

    /// <summary>
    /// Returns <see langword="true" /> if the transaction contains the specified stream.
    /// </summary>
    bool ContainsStream(string streamId);

    /// <summary>
    /// Removes the stream from the transaction if it exists.
    /// </summary>
    bool Remove(string streamId);

    /// <summary>
    /// Gets the number of streams in the transaction.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the stream ids in the transaction.
    /// </summary>
    IReadOnlyList<string> StreamIds { get; }

    /// <summary>
    /// Returns <see langword="true" /> if the transaction contains the specified stream.
    /// </summary>
    /// <param name="streamId">Stream Id</param>
    /// <param name="value">The current events list, if found</param>
    /// <returns></returns>
    bool TryGetStream(string streamId, [MaybeNullWhen(false)] out EventsList value);

    /// <summary>
    /// Clears the transaction.
    /// </summary>
    void Clear();
}