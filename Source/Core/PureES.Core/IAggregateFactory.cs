namespace PureES.Core;

public interface IAggregateFactory<TAggregate>
{
    Task<TAggregate> Create(IAsyncEnumerable<EventEnvelope> events, CancellationToken ct);

    Task<TAggregate> Update(TAggregate aggregate, IAsyncEnumerable<EventEnvelope> events, CancellationToken ct);
}