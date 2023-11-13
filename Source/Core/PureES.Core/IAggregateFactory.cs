namespace PureES.Core;

public interface IAggregateFactory<TAggregate>
{
    /// <summary>
    /// Creates a new aggregate from the given events
    /// </summary>
    Task<TAggregate> Create(string streamId, IAsyncEnumerable<EventEnvelope> events, CancellationToken ct);

    /// <summary>
    /// Updates an existing aggregate from the given events
    /// </summary>
    Task<TAggregate> Update(string streamId, TAggregate current, IAsyncEnumerable<EventEnvelope> events, CancellationToken ct);
}