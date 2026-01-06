// ReSharper disable UnusedMember.Global

namespace PureES;

[PublicAPI]
public static class EventStoreReadExtensions
{
    /// <param name="eventStore">Event store</param>
    extension(IEventStore eventStore)
    {
        /// <summary>
        ///     Reads all events forwards in chronological order
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>
        ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
        ///     from all events in the order in which they were added
        /// </returns>
        public IAsyncEnumerable<EventEnvelope> ReadAll(CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.ReadAll(Direction.Forwards, cancellationToken);
        }

        /// <summary>
        ///     Reads all events in chronological order
        /// </summary>
        /// <param name="maxCount">The maximum number of events to return</param>
        /// <param name="cancellationToken"></param>
        /// <returns>
        ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
        ///     from all events in the order in which they were added
        /// </returns>
        public IAsyncEnumerable<EventEnvelope> ReadAll(uint maxCount,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.ReadAll(Direction.Forwards, maxCount, cancellationToken);
        }

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
        public IEventStoreStream Read(string streamId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.Read(Direction.Forwards, streamId, cancellationToken);
        }

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
        public IEventStoreStream Read(string streamId,
            uint expectedRevision,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.Read(Direction.Forwards, streamId, expectedRevision, cancellationToken);
        }

        /// <summary>
        ///     Reads events from stream <paramref name="streamId" />,
        ///     ensuring stream is at <see cref="expectedRevision" />
        /// </summary>
        /// <param name="streamId">Id of stream to load events from</param>
        /// <param name="startRevision">Revision to start reading from</param>
        /// <param name="expectedRevision"><c>Revision</c> that the stream is expected to be at</param>
        /// <param name="cancellationToken"></param>
        /// <returns>
        ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
        ///     from the events in the stream in the order in which they were added
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startRevision" /> &gt; <paramref name="expectedRevision"/></exception>
        /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
        /// <exception cref="WrongStreamRevisionException">
        ///     Stream <paramref name="streamId" /> not at revision <paramref name="expectedRevision" />
        /// </exception>
        public IEventStoreStream Read(string streamId,
            uint startRevision,
            uint expectedRevision,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.Read(Direction.Forwards, streamId, startRevision, expectedRevision, cancellationToken);
        }

        /// <summary>
        ///     Loads events from stream <paramref name="streamId" />, returning at most <paramref name="count" /> elements
        /// </summary>
        /// <param name="streamId">Id of stream to load events from</param>
        /// <param name="count">Maximum number of events to read</param>
        /// <param name="cancellationToken"></param>
        /// <returns>
        ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
        ///     from the events in the stream in the order in which they were added,
        ///     up to and including event at <paramref name="count" />
        /// </returns>
        /// <exception cref="StreamNotFoundException">Stream <paramref name="streamId" /> not found</exception>
        /// <exception cref="WrongStreamRevisionException">
        ///     Revision of stream <paramref name="streamId" /> less than <paramref name="count" />
        /// </exception>
        public IEventStoreStream ReadPartial(string streamId,
            uint count,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.ReadPartial(Direction.Forwards, streamId, count, cancellationToken);
        }

        /// <summary>
        /// Reads multiple streams as a single operation. Order of streams is undefined.
        /// </summary>
        /// <param name="streams">The streams to read</param>
        /// <param name="cancellationToken"></param>
        public IAsyncEnumerable<IEventStoreStream> ReadMany(IEnumerable<string> streams,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.ReadMany(Direction.Forwards, streams, cancellationToken);
        }

        /// <summary>
        /// Reads multiple streams as a single operation. Order of streams is undefined.
        /// </summary>
        /// <param name="streams">The streams to read</param>
        /// <param name="cancellationToken"></param>
        public IAsyncEnumerable<IEventStoreStream> ReadMany(IAsyncEnumerable<string> streams,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.ReadMany(Direction.Forwards, streams, cancellationToken);
        }

        /// <summary>
        ///     Loads events by <c>EventType</c>
        /// </summary>
        /// <param name="eventType">Type of event to query</param>
        /// <param name="cancellationToken"></param>
        /// <returns>
        ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
        ///     from the events in the stream in the order in which they were added
        /// </returns>
        public IAsyncEnumerable<EventEnvelope> ReadByEventType(Type eventType,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.ReadByEventType(Direction.Forwards, [eventType], cancellationToken);
        }

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
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.ReadByEventType(direction, [eventType], cancellationToken);
        }

        /// <summary>
        ///     Loads events by <c>EventType</c>
        /// </summary>
        /// <param name="eventType">Type of event to query</param>
        /// <param name="maxCount">The maximum number of events to return</param>
        /// <param name="cancellationToken"></param>
        /// <returns>
        ///     An <see cref="IAsyncEnumerable{T}" /> of <see cref="EventEnvelope" />
        ///     from the events in the stream in the order in which they were added
        /// </returns>
        public IAsyncEnumerable<EventEnvelope> ReadByEventType(Type eventType,
            uint maxCount,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventStore);
            return eventStore.ReadByEventType(Direction.Forwards, [eventType], maxCount, cancellationToken);
        }
    }
}