namespace PureES.Core.ExpBuilders.WhenHandlers;

internal delegate ValueTask<LoadedAggregate<T>> AggregateFactory<T>(
    IAsyncEnumerable<EventEnvelope> @events, 
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken);