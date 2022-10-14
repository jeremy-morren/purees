using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace PureES.EventBus;

internal class CompositeEventHandler<TEvent, TMetadata> : IEventHandler<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    private readonly IServiceProvider _services;
    private readonly EventHandlerCollection _handlers;
    private readonly IOptions<EventBusOptions> _options;
    private readonly ILogger _logger;

    public CompositeEventHandler(IServiceProvider services,
        EventHandlerCollection handlers,
        ILoggerFactory loggerFactory,
        IOptions<EventBusOptions> options)
    {
        _services = services;
        _handlers = handlers;
        _options = options;
        _logger = loggerFactory.CreateLogger($"PureES.EventBus.CompositeEventHandler<{typeof(TEvent)}>");
    }

    public Task Handle(EventEnvelope<TEvent, TMetadata> @event, CancellationToken ct)
    {
        var factories = _handlers.Get<TEvent, TMetadata>();
        if (factories == null) return Task.CompletedTask;
        //Execute all handlers in separate scopes
        //This avoids interference due to race conditions
        return Task.WhenAll(factories.Select(async f =>
        {
            await using var scope = _services.CreateAsyncScope();
            try
            {
                _logger.LogDebug("Handling projection for event {@Event}", typeof(TEvent));
                await f(scope.ServiceProvider).Handle(@event, ct);
                _logger.LogInformation("Successfully handled projection for event {@Event}", typeof(TEvent));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing projection for event {@Event}", typeof(TEvent));
                if (_options.Value.PropagateEventHandlerExceptions)
                    throw;
                //This allows suppressing exceptions if that is desired
            }
        }));
    }
}