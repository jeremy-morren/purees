namespace PureES.Core.ExpBuilders.WhenHandlers;

internal delegate Task<LoadedAggregate<T>> AggregateFactory<T>(
    IAsyncEnumerable<EventEnvelope> @events, 
    CancellationToken cancellationToken);