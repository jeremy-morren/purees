using Microsoft.Extensions.DependencyInjection;
using PureES.Core.EventStore;
using PureES.Core.ExpBuilders.WhenHandlers;

namespace PureES.Core.ExpBuilders.Services;

internal class AggregateStore<TAggregate> : IAggregateStore<TAggregate> where TAggregate : notnull
{
    private readonly PureESServices _services;
    private readonly IEventStore _eventStore;

    public AggregateStore(PureESServices services, 
        IEventStore eventStore)
    {
        _services = services;
        _eventStore = eventStore;
    }

    private AggregateFactory<TAggregate> Factory =>
        _services.GetService<AggregateFactory<TAggregate>>()
        ?? throw new InvalidOperationException(
            $"Could not locate factory for aggregate {typeof(TAggregate)}");

    public Task<LoadedAggregate<TAggregate>> Create(IAsyncEnumerable<EventEnvelope> events, CancellationToken token) 
        => Factory(events, token);

    public Task<LoadedAggregate<TAggregate>> Load(string streamId, CancellationToken token) 
        => Factory(_eventStore.Load(streamId, token), token);

    public Task<LoadedAggregate<TAggregate>> Load(string streamId, ulong expectedVersion, CancellationToken token)
        => Factory(_eventStore.Load(streamId, expectedVersion, token), token);

    public Task<LoadedAggregate<TAggregate>> LoadPartial(string streamId, ulong requiredVersion, CancellationToken token) 
        => Factory(_eventStore.LoadPartial(streamId, requiredVersion, token), token);
}