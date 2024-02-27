namespace PureES;

[PublicAPI]
public interface IAggregateStore<T> where T : notnull
{
    Task<T> Load(string streamId, CancellationToken cancellationToken);
    Task<T> Load(string streamId, ulong expectedRevision, CancellationToken cancellationToken);
    Task<T> LoadAt(string streamId, ulong endRevision, CancellationToken cancellationToken);
}