using System.Runtime.CompilerServices;

namespace PureES;

/// <summary>
/// The default implementation of <see cref="IAggregateStore{T}"/>
/// </summary>
/// <remarks>
/// Does not include any snapshotting
/// </remarks>
/// <typeparam name="TAggregate"></typeparam>
internal class BasicAggregateStore<TAggregate> : IAggregateStore<TAggregate> where TAggregate : notnull
{
    private readonly IEventStore _eventStore;
    private readonly IAggregateFactory<TAggregate> _factory;

    public BasicAggregateStore(IEventStore eventStore,
        IAggregateFactory<TAggregate> factory)
    {
        _eventStore = eventStore;
        _factory = factory;
    }

    public ValueTask<TAggregate> Load(string streamId, CancellationToken cancellationToken)
    {
        var stream = _eventStore.Read(Direction.Forwards, streamId, cancellationToken);
        return this.RehydrateAggregate(stream, _factory, cancellationToken);
    }

    public ValueTask<TAggregate> Load(string streamId, uint expectedRevision, CancellationToken cancellationToken)
    {
        var stream = _eventStore.Read(Direction.Forwards, streamId, expectedRevision, cancellationToken);
        return this.RehydrateAggregate(stream, _factory, cancellationToken);
    }

    public ValueTask<TAggregate> LoadAt(string streamId, uint endRevision, CancellationToken cancellationToken)
    {
        var stream = _eventStore.ReadSlice(streamId, 0, endRevision, cancellationToken);
        return this.RehydrateAggregate(stream, _factory, cancellationToken);
    }

    public async IAsyncEnumerable<TAggregate> LoadMany(IEnumerable<string> streamIds, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var stream in _eventStore.ReadMany(streamIds, cancellationToken))
        {
            var result = await this.RehydrateAggregate(stream, _factory, cancellationToken);
            yield return result;
        }
    }

    public async IAsyncEnumerable<TAggregate> LoadMany(IAsyncEnumerable<string> streamIds, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var stream in _eventStore.ReadMany(streamIds, cancellationToken))
        {
            var result = await this.RehydrateAggregate(stream, _factory, cancellationToken);
            yield return result;
        }
    }
}