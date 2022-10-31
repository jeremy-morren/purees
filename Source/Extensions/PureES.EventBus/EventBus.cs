using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;

namespace PureES.EventBus;

internal class EventBus : IEventBus
{
    private readonly IServiceProvider _services;
    private readonly EventHandlerCollection _eventHandlers;
    private readonly IOptions<EventBusOptions> _options;
    private readonly ILogger<EventBus> _logger;

    public EventBus(IServiceProvider services,
        EventHandlerCollection eventHandlers,
        IOptions<EventBusOptions> options,
        ILogger<EventBus> logger)
    {
        _services = services;
        _eventHandlers = eventHandlers;
        _options = options;
        _logger = logger;
    }

    public Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>[] GetRegisteredEventHandlers<TEvent, TMetadata>()
        where TEvent : notnull
        where TMetadata : notnull =>
        _eventHandlers.Get<TEvent, TMetadata>() ?? Array.Empty<Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>>();

    private static readonly ConcurrentDictionary<Type, Func<EventBus, EventEnvelope, CancellationToken, Task>> PublishHandlers = new();

    public Task Publish(EventEnvelope envelope, CancellationToken ct)
    {
        var handler = PublishHandlers.GetOrAdd(envelope.Event.GetType(), 
            t => CompileDelegate(t, envelope.Metadata?.GetType() ?? typeof(object)));
        return handler(this, envelope, ct);
    }
    
    private Func<EventBus, EventEnvelope, CancellationToken, Task> CompileDelegate(Type eventType, Type metadataType)
    {
        var busParam = Expression.Parameter(typeof(EventBus), "eventBus");
        var envelopeParam = Expression.Parameter(typeof(EventEnvelope), "envelope");
        var tokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var method = GetType().GetMethod(nameof(PublishGeneric), 
                         BindingFlags.NonPublic | BindingFlags.Static) 
                     ?? throw new InvalidOperationException($"Unable to get {nameof(PublishGeneric)} method");
        method = method.MakeGenericMethod(eventType, metadataType);
        //Looks like (bus, envelope, ct) => Publish<T,T>(bus,envelope,ct)
        var call = Expression.Call(method, busParam, envelopeParam, tokenParam);
        return Expression.Lambda<Func<EventBus, EventEnvelope, CancellationToken, Task>>(call, 
            busParam, envelopeParam, tokenParam).Compile();
    }
    
    #region Publish

    private static async Task PublishGeneric<TEvent, TMetadata>(EventBus bus, EventEnvelope envelope, CancellationToken ct) 
        where TEvent : notnull
        where TMetadata : notnull
    {
        var factories = bus.GetRegisteredEventHandlers<TEvent, TMetadata>();
        foreach (var f in factories)
        {
            await using var scope = bus._services.CreateAsyncScope();
            try
            {
                bus._logger.LogDebug("Handling projection for event {@Event}", typeof(TEvent));
                await f(scope.ServiceProvider).Handle(new EventEnvelope<TEvent, TMetadata>(envelope), ct);
                bus._logger.LogInformation("Successfully handled projection for event {@Event}", typeof(TEvent));
            }
            catch (Exception e)
            {
                bus._logger.LogError(e, "Error processing projection for event {@Event}", typeof(TEvent));
                if (bus._options.Value.PropagateEventHandlerExceptions)
                    throw;
                //This allows suppressing exceptions if that is desired
            }
        }
    }

    #endregion
}
