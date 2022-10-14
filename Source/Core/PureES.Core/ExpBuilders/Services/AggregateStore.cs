using Microsoft.Extensions.DependencyInjection;
using PureES.Core.EventStore;
using PureES.Core.ExpBuilders.WhenHandlers;

namespace PureES.Core.ExpBuilders.Services;

internal class AggregateStore<TAggregate> : IAggregateStore<TAggregate> where TAggregate : notnull
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PureESServices _services;
    private readonly IEventStore _eventStore;

    public AggregateStore(IServiceProvider serviceProvider,
        PureESServices services, 
        IEventStore eventStore)
    {
        _serviceProvider = serviceProvider;
        _services = services;
        _eventStore = eventStore;
    }

    private AggregateFactory<TAggregate> Factory =>
        _services.GetService<AggregateFactory<TAggregate>>()
        ?? throw new InvalidOperationException(
            $"Could not locate factory for aggregate {typeof(TAggregate)}");

    public ValueTask<LoadedAggregate<TAggregate>> Create(IAsyncEnumerable<EventEnvelope> events, CancellationToken token) 
        => Factory(events, _serviceProvider, token);

    public ValueTask<LoadedAggregate<TAggregate>> Load(string streamId, CancellationToken token) 
        => Factory(_eventStore.Read(streamId, token), _serviceProvider, token);

    public ValueTask<LoadedAggregate<TAggregate>> Load(string streamId, ulong expectedVersion, CancellationToken token)
        => Factory(_eventStore.Read(streamId, expectedVersion, token), _serviceProvider, token);

    public ValueTask<LoadedAggregate<TAggregate>> LoadPartial(string streamId, ulong requiredVersion, CancellationToken token) 
        => Factory(_eventStore.ReadPartial(streamId, requiredVersion, token), _serviceProvider, token);
}