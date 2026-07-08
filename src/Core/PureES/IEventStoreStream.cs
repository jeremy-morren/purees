namespace PureES;

/// <summary>
/// Represents a stream of events in the event store.
/// </summary>
public interface IEventStoreStream : IAsyncEnumerable<EventEnvelope>
{
    /// <summary>
    /// The ID of the stream
    /// </summary>
    string StreamId { get; }

    /// <summary>
    /// The direction that the stream is being read in
    /// </summary>
    Direction Direction { get; }
}