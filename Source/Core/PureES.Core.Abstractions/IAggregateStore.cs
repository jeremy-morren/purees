namespace PureES.Core;

[PublicAPI]
public interface IAggregateStore<T> where T : notnull
{
    Task<T> Create(IAsyncEnumerable<EventEnvelope> events, CancellationToken cancellationToken);
    Task<T> Load(string streamId, CancellationToken cancellationToken);
    Task<T> Load(string streamId, ulong expectedRevision, CancellationToken cancellationToken);
    Task<T> LoadPartial(string streamId, ulong requiredRevision, CancellationToken cancellationToken);
}