namespace PureES;

/// <summary>
/// Rehydrates aggregates from the event store
/// </summary>
/// <typeparam name="TAggregate">The aggregate type</typeparam>
[PublicAPI]
public interface IAggregateStore<TAggregate> where TAggregate : notnull
{
    /// <summary>
    /// Load an aggregate from the event store
    /// </summary>
    /// <param name="streamId">Aggregate stream Id</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<TAggregate> Load(string streamId, CancellationToken cancellationToken);

    /// <summary>
    /// Load an aggregate from the event store with an expected revision
    /// </summary>
    /// <param name="streamId">Aggregate stream id</param>
    /// <param name="expectedRevision">The revision that the stream should be at</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<TAggregate> Load(string streamId, uint expectedRevision, CancellationToken cancellationToken);

    /// <summary>
    /// Load an aggregate from the event store at a specific revision
    /// </summary>
    /// <param name="streamId">Aggregate stream id</param>
    /// <param name="endRevision">The end stream position to read to</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<TAggregate> LoadAt(string streamId, uint endRevision, CancellationToken cancellationToken);

    /// <summary>
    /// Load multiple aggregates from the event store
    /// </summary>
    /// <param name="streamIds">Stream ids to load</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<TAggregate> LoadMany(IEnumerable<string> streamIds, CancellationToken cancellationToken);

    /// <summary>
    /// Load multiple aggregates from the event store
    /// </summary>
    /// <param name="streamIds">Stream ids to load</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<TAggregate> LoadMany(IAsyncEnumerable<string> streamIds, CancellationToken cancellationToken);
}