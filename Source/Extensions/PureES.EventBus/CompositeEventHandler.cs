using Microsoft.Extensions.Logging;
using PureES.Core;

// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace PureES.EventBus;

public class CompositeEventHandler<TEvent, TMetadata> : IEventHandler<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    private readonly ILogger _logger;
    private readonly IEventHandler<TEvent, TMetadata>[] _handlers;

    public CompositeEventHandler(IServiceProvider provider,
        Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>[] handlers,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger($"PureES.EventBus.CompositeEventHandler<{typeof(TEvent)}>");
        _handlers = handlers.Select(h => h(provider)).ToArray();
    }

    public Task Handle(EventEnvelope<TEvent, TMetadata> @event, CancellationToken ct)
    {
        var tasks = _handlers
            .Select(async h =>
            {
                try
                {
                    _logger.LogDebug("Handling projections for event {@Event}", typeof(TEvent));
                    await h.Handle(@event, ct);
                    _logger.LogInformation("Successfully handled projections for event {@Event}", typeof(TEvent));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error processing projection for event {@Event}", typeof(TEvent));
                    //We don't rethrow because projections should not fail
                }
            });
        return Task.WhenAll(tasks);
    }
}