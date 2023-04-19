using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PureES.Core;

namespace PureES.EventBus;

internal class EventBus : IEventBus
{
    private static readonly ConcurrentDictionary<Type, Func<EventBus, EventEnvelope, CancellationToken, Task>>
        PublishHandlers = new();

    private readonly ILogger<EventBus> _logger;
    private readonly IOptions<EventBusOptions> _options;
    private readonly IServiceProvider _services;

    public EventBus(IServiceProvider services,
        IOptions<EventBusOptions> options,
        ILogger<EventBus>? logger = null)
    {
        _services = services;
        _options = options;
        _logger = logger ?? NullLogger<EventBus>.Instance;
    }

    public IEnumerable<Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>> GetRegisteredEventHandlers<TEvent, TMetadata>()
        where TEvent : notnull
        where TMetadata : notnull =>
        _services.GetService<IEnumerable<Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>>>() 
            ?? Enumerable.Empty<Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>>();

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

    private static async Task PublishGeneric<TEvent, TMetadata>(EventBus bus, EventEnvelope envelope,
        CancellationToken ct)
        where TEvent : notnull
        where TMetadata : notnull
    {
        var handlers = bus.GetRegisteredEventHandlers<TEvent, TMetadata>();
        foreach (var handler in handlers)
        {
            await using var scope = bus._services.CreateAsyncScope();
            var eventProps = new {envelope.StreamId, envelope.StreamPosition, Type = typeof(TEvent)};
            var start = Stopwatch.GetTimestamp();
            try
            {
                bus._logger.LogDebug("Handling projection for event {@Event}", eventProps);
                await handler(scope.ServiceProvider).Handle(new EventEnvelope<TEvent, TMetadata>(envelope), ct);
                var elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                var level = bus._options.Value.GetLogLevel(envelope, null, elapsedMs);
                bus._logger.Log(level,
                    "Successfully handled projection for event {@Event}. Elapsed: {Elapsed:0.0000} ms", eventProps,
                    elapsedMs);
            }
            catch (Exception e)
            {
                var elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                var level = bus._options.Value.GetLogLevel(envelope, e, elapsedMs);
                bus._logger.Log(level, e, "Error processing projection for event {@Event}. Elapsed: {Elapsed:0.0000} ms",
                    eventProps, elapsedMs);
                if (bus._options.Value.PropagateEventHandlerExceptions)
                    throw;
                //This allows suppressing exceptions if that is desired
            }
        }
    }

    private static double GetElapsedMilliseconds(long start, long stop) =>
        (stop - start) * 1000 / (double) Stopwatch.Frequency;
}