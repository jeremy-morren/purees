namespace PureES.Core;

public interface IAggregateStore<T>
{
    Task<LoadedAggregate<T>> Create(IAsyncEnumerable<EventEnvelope> @events, CancellationToken token);
    Task<LoadedAggregate<T>> Load(string streamId, CancellationToken token);
    Task<LoadedAggregate<T>> Load(string streamId, ulong expectedVersion, CancellationToken token);
    Task<LoadedAggregate<T>> LoadPartial(string streamId, ulong requiredVersion, CancellationToken token);
}