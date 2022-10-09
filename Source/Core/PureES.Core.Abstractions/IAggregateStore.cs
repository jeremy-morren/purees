namespace PureES.Core;

public interface IAggregateStore<T> where T : notnull
{
    ValueTask<LoadedAggregate<T>> Create(IAsyncEnumerable<EventEnvelope> @events, CancellationToken token);
    ValueTask<LoadedAggregate<T>> Load(string streamId, CancellationToken token);
    ValueTask<LoadedAggregate<T>> Load(string streamId, ulong expectedVersion, CancellationToken token);
    ValueTask<LoadedAggregate<T>> LoadPartial(string streamId, ulong requiredVersion, CancellationToken token);
}