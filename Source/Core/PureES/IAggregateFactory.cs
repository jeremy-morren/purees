namespace PureES;

/// <summary>
/// A factory for creating and updating aggregates from event streams
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public interface IAggregateFactory<T>
{
    /// <summary>
    /// Creates a new aggregate from the given event stream
    /// </summary>
    Task<RehydratedAggregate<T>> Create(string streamId, IAsyncEnumerable<EventEnvelope> events, CancellationToken ct);

    /// <summary>
    /// Updates an existing aggregate from the given event stream
    /// </summary>
    Task<RehydratedAggregate<T>> Update(string streamId, RehydratedAggregate<T> current, IAsyncEnumerable<EventEnvelope> events, CancellationToken ct);
}