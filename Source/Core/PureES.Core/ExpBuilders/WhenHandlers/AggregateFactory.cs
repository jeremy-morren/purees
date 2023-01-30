namespace PureES.Core.ExpBuilders.WhenHandlers;

internal delegate ValueTask<T> AggregateFactory<T>(
    IAsyncEnumerable<EventEnvelope> events,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken);