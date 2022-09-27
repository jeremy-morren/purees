namespace PureES.Core;

public delegate Task<LoadedAggregate<T>> AggregateFactory<T>(IAsyncEnumerable<EventEnvelope> @events, 
    CancellationToken cancellationToken);