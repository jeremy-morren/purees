namespace PureES;

public interface IAggregateFactory<T>
{
    /// <summary>
    /// Creates a new aggregate from the given events
    /// </summary>
    Task<RehydratedAggregate<T>> Create(string streamId, IAsyncEnumerable<EventEnvelope> events, CancellationToken ct);

    /// <summary>
    /// Updates an existing aggregate from the given events
    /// </summary>
    Task<RehydratedAggregate<T>> Update(string streamId, RehydratedAggregate<T> current, IAsyncEnumerable<EventEnvelope> events, CancellationToken ct);
}