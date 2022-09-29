using System.Collections.Concurrent;
using PureES.Core.EventStore;

namespace PureES.Core;

internal class AggregateStore<TAggregate> : IAggregateStore<TAggregate> where TAggregate : notnull
{
    private readonly AggregateFactory<TAggregate> _factory;
    private readonly IEventStore _eventStore;

    public AggregateStore(AggregateFactory<TAggregate> factory, 
        IEventStore eventStore)
    {
        _factory = factory;
        _eventStore = eventStore;
    }

    public Task<LoadedAggregate<TAggregate>> Create(IAsyncEnumerable<EventEnvelope> events, CancellationToken token) 
        => _factory(events, token);

    public Task<LoadedAggregate<TAggregate>> Load(string streamId, CancellationToken token) 
        => _factory(_eventStore.Load(streamId, token), token);

    public Task<LoadedAggregate<TAggregate>> Load(string streamId, ulong expectedVersion, CancellationToken token)
        => _factory(_eventStore.Load(streamId, expectedVersion, token), token);

    public Task<LoadedAggregate<TAggregate>> LoadPartial(string streamId, ulong requiredVersion, CancellationToken token) 
        => _factory(_eventStore.LoadPartial(streamId, requiredVersion, token), token);
}