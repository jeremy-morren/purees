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

    public async Task<TAggregate> Load(string streamId, CancellationToken cancellationToken)
    {
        var result = await _factory.Create(streamId, 
            _eventStore.Read(Direction.Forwards, streamId, cancellationToken),
            cancellationToken);
        return result.Aggregate;
    }

    public async Task<TAggregate> Load(string streamId, ulong expectedRevision, CancellationToken cancellationToken)
    {
        var result = await _factory.Create(streamId,
            _eventStore.Read(Direction.Forwards, streamId, expectedRevision, cancellationToken), 
            cancellationToken);
        return result.Aggregate;
    }

    public async Task<TAggregate> LoadSlice(string streamId, ulong endRevision, CancellationToken cancellationToken)
    {
        var result = await _factory.Create(streamId, 
            _eventStore.ReadSlice(streamId, 0, endRevision, cancellationToken),
            cancellationToken);
        return result.Aggregate;
    }
}