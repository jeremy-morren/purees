namespace PureES.Core;

public interface IAggregateStore<T> where T : notnull
{
    ValueTask<T> Create(IAsyncEnumerable<EventEnvelope> events, CancellationToken cancellationToken);
    ValueTask<T> Load(string streamId, CancellationToken cancellationToken);
    ValueTask<T> Load(string streamId, ulong expectedRevision, CancellationToken cancellationToken);
    ValueTask<T> LoadPartial(string streamId, ulong requiredRevision, CancellationToken cancellationToken);
}