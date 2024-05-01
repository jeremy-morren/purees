using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace PureES.EventBus;

public class EventBus : IEventBus
{
    private readonly EventStreamBlock _handler;

    private readonly ILogger<EventBus> _logger;
    private readonly IServiceProvider _services;

    public EventBus(
        IServiceProvider services,
        ILogger<EventBus>? logger = null)
    {
        _services = services;
        _logger = logger ?? NullLogger<EventBus>.Instance;
        
        _handler = new EventStreamBlock(Publish);

        var onHandled = new ActionBlock<EventEnvelope>(OnEventHandled, 
            new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true,
                BoundedCapacity = DataflowBlockOptions.Unbounded, //No backpressure after handle
                MaxDegreeOfParallelism = 1 //Handle 1 at a time
            });
        _handler.LinkTo(onHandled, new DataflowLinkOptions() {PropagateCompletion = true});
        
        Completion = onHandled.Completion;
    }

    #region Handle

    private static readonly ConcurrentDictionary<Type, Type> EventHandlerTypes = new();

    private async Task Publish(EventEnvelope envelope)
    {
        var logEvent = new
        {
            envelope.StreamId, 
            envelope.StreamPosition, 
            EventType = GetTypeName(GetEventType(envelope))
        };
        
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var serviceType = EventHandlerTypes.GetOrAdd(envelope.Event.GetType(), 
                t =>
                {
                    t = typeof(IEventHandler<>).MakeGenericType(t);
                    return typeof(IEnumerable<>).MakeGenericType(t);
                });

            var handlers = new List<IEventHandler>();
            var genericHandlers = (IEnumerable?)scope.ServiceProvider.GetService(serviceType);
            if (genericHandlers != null)
                handlers.AddRange(genericHandlers.OfType<IEventHandler>());
            
            var catchAllHandlers = scope.ServiceProvider.GetService<IEnumerable<IEventHandler>>();
            if (catchAllHandlers != null)
                handlers.AddRange(catchAllHandlers);
        
            if (handlers.Count == 0) return;
    
            _logger.LogDebug("Processing {EventHandlerCount} event handler(s) for event {@Event}", 
                handlers.Count, logEvent);
            var start = Stopwatch.GetTimestamp();
            
            foreach (var handler in handlers.OrderBy(h => h.Priority))
                await handler.Handle(envelope);
            
            var elapsed = GetElapsed(start);
            _logger.LogDebug(
                "Processed {EventHandlerCount} event handler(s) for event {@Event}. Elapsed: {Elapsed:0.0000} ms",
                handlers.Count, logEvent, elapsed.TotalMilliseconds);
        }
        catch (Exception e)
        {
            //Either DI failure, or event handler propagated exception
            _logger.LogCritical(e, "An error occurred handling event {@LogEvent}", logEvent);
            throw;
        }
    }

    private static TimeSpan GetElapsed(long start)
    {
        var seconds = (Stopwatch.GetTimestamp() - start) / (double) Stopwatch.Frequency;
        return TimeSpan.FromSeconds(seconds);
    }

    private static Type GetEventType(EventEnvelope envelope) => 
        envelope.Event?.GetType() ?? throw new InvalidOperationException("Event is null");

    private static string GetTypeName(MemberInfo type)
    {
        return type.DeclaringType != null ? $"{GetTypeName(type.DeclaringType)}+{type.Name}" : type.Name;
    }
    
    #endregion
    
    #region Events
    
    private async Task OnEventHandled(EventEnvelope envelope)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var handlers = scope.ServiceProvider
                .GetService<IEnumerable<IEventBusEvents>>()
                ?.ToList();
            if (handlers == null || handlers.Count == 0)
                return;
            
            _logger.LogDebug("Processing OnEventHandled for {Handlers} handler(s)", handlers.Count);
            await Task.WhenAll(handlers.Select(h => h.OnEventHandled(envelope)));
            
            _logger.LogDebug("Processed OnEventHandled events for {Handlers} handler(s)", handlers.Count);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing OnEventHandled events");
            throw;
        }
    }

    #endregion

    #region Dataflow
    
    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader,
        EventEnvelope messageValue,
        ISourceBlock<EventEnvelope>? source, 
        bool consumeToAccept) =>
        _handler.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    public void Complete()
    {
        _handler.Complete();
    }

    public void Fault(Exception exception)
    {
        _handler.Fault(exception);
    }

    public Task Completion { get; }

    #endregion
}