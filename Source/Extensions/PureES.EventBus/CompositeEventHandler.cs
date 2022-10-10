using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;

// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace PureES.EventBus;

internal class CompositeEventHandler<TEvent, TMetadata> : IEventHandler<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    private readonly IOptions<EventBusOptions> _options;
    private readonly ILogger _logger;
    private readonly IEventHandler<TEvent, TMetadata>[]? _handlers;

    public CompositeEventHandler(IServiceProvider provider,
        EventHandlerCollection handlers,
        ILoggerFactory loggerFactory,
        IOptions<EventBusOptions> options)
    {
        _options = options;
        _logger = loggerFactory.CreateLogger($"PureES.EventBus.CompositeEventHandler<{typeof(TEvent)}>");
        _handlers = handlers.Resolve<TEvent, TMetadata>(provider);
    }

    public Task Handle(EventEnvelope<TEvent, TMetadata> @event, CancellationToken ct)
    {
        if (_handlers == null) return Task.CompletedTask;
        var tasks = _handlers
            .Select(async h =>
            {
                try
                {
                    _logger.LogDebug("Handling projection for event {@Event}", typeof(TEvent));
                    await h.Handle(@event, ct);
                    _logger.LogInformation("Successfully handled projection for event {@Event}", typeof(TEvent));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error processing projection for event {@Event}", typeof(TEvent));
                    if (_options.Value.PropagateEventHandlerExceptions)
                        throw;
                    //This allows suppressing exceptions if that is desired
                }
            });
        return Task.WhenAll(tasks);
    }
}