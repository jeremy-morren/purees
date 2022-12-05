namespace PureES.Core;

public interface IAggregateStore<T> where T : notnull
{
    ValueTask<LoadedAggregate<T>> Create(IAsyncEnumerable<EventEnvelope> events, CancellationToken cancellationToken);
    ValueTask<LoadedAggregate<T>> Load(string streamId, CancellationToken cancellationToken);
    ValueTask<LoadedAggregate<T>> Load(string streamId, ulong expectedRevision, CancellationToken cancellationToken);
    ValueTask<LoadedAggregate<T>> LoadPartial(string streamId, ulong requiredRevision, CancellationToken cancellationToken);
}