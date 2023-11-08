namespace PureES.Core;

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

    public Task<TAggregate> Load(string streamId, CancellationToken cancellationToken) =>
        _factory.Create(_eventStore.Read(Direction.Forwards, streamId, cancellationToken), cancellationToken);

    public Task<TAggregate> Load(string streamId, ulong expectedRevision, CancellationToken cancellationToken) =>
        _factory.Create(_eventStore.Read(Direction.Forwards, streamId, expectedRevision, cancellationToken), cancellationToken);

    public Task<TAggregate> LoadPartial(string streamId, ulong requiredRevision, CancellationToken cancellationToken) =>
        _factory.Create(_eventStore.ReadPartial(Direction.Forwards, streamId, requiredRevision, cancellationToken), cancellationToken);
}