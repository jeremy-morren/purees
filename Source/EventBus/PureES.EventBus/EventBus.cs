using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PureES.Core;

// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace PureES.EventBus;

public class EventBus : IEventBus
{
    private readonly ITargetBlock<EventEnvelope> _targetBlock;

    private readonly ILogger<EventBus> _logger;
    private readonly EventBusOptions _options;
    private readonly IEventHandlersCollection _eventHandlers;
    private readonly IServiceProvider _services;

    public EventBus(EventBusOptions options,
        IServiceProvider services,
        IEventHandlersCollection eventHandlers,
        ILogger<EventBus>? logger = null)
    {
        if (options.EventHandlerTimeout.Ticks <= 0)
            throw new InvalidOperationException($"{nameof(options.EventHandlerTimeout)} must be greater than 0");
        _options = options;
        
        _services = services;
        _eventHandlers = eventHandlers;
        _logger = logger ?? NullLogger<EventBus>.Instance;
        
        var handler = new EventStreamBlock(Publish, _options);
        
        _targetBlock = handler;

        var onHandled = new ActionBlock<EventEnvelope>(OnEventHandled, 
            new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true,
                BoundedCapacity = DataflowBlockOptions.Unbounded, //No backpressure after handle
                MaxDegreeOfParallelism = 1 //Handle 1 at a time
            });
        handler.LinkTo(onHandled, new DataflowLinkOptions() {PropagateCompletion = true});
        Completion = onHandled.Completion;
    }

    #region Handle

    private async Task Publish(EventEnvelope envelope)
    {
        using var activity = new Activity("PureES.EventBus.Handle");
        activity.SetTag(nameof(envelope.StreamId), envelope.StreamId);
        activity.SetTag(nameof(envelope.StreamPosition), envelope.StreamPosition);
        activity.SetTag("EventType", GetTypeName(GetEventType(envelope)));
        activity.Start();
        Activity.Current = activity;
        
        var handlers = _eventHandlers.GetEventHandlers(GetEventType(envelope));
        if (handlers.Length == 0) return;
    
        var logEvent = new
        {
            envelope.StreamId, 
            envelope.StreamPosition, 
            EventType = GetTypeName(GetEventType(envelope))
        };
        _logger.LogDebug("Processing {EventHandlerCount} event handler(s) for event {@Event}", 
            handlers.Length, logEvent);
        var start = Stopwatch.GetTimestamp();
        foreach (var handler in handlers)
            await Handle(envelope, handler);
        var elapsed = GetElapsed(start);
        _logger.LogDebug(
            "Processed {EventHandlerCount} event handler(s) for event {@Event}. Elapsed: {Elapsed:0.0000} ms",
            handlers.Length, logEvent, elapsed.TotalMilliseconds);
    }
    
    private async Task Handle(EventEnvelope envelope, EventHandlerDelegate handler)
    {
        using var activity = new Activity("PureES.EventBus.Handle");
        activity.SetTag(nameof(envelope.StreamId), envelope.StreamId);
        activity.SetTag(nameof(envelope.StreamPosition), envelope.StreamPosition);
        activity.SetTag("EventType", GetTypeName(GetEventType(envelope)));
        activity.SetTag("EventHandler", handler.Name);
        Activity.Current = activity;
        activity.Start();
        
        var logEvent = new
        {
            envelope.StreamId, 
            envelope.StreamPosition, 
            EventType = GetTypeName(GetEventType(envelope))
        };
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            {"EventHandler", handler.Name},
            {"EventType", GetEventType(envelope)},
            {"StreamId", envelope.StreamId},
            {"StreamPosition", envelope.StreamPosition}
        });

        var ct = new CancellationTokenSource(_options.EventHandlerTimeout).Token;
        
        await using var scope = _services.CreateAsyncScope();
        _logger.LogDebug("Invoking event handler {EventHandler} for event {@Event}", handler.Name, logEvent);
        var start = Stopwatch.GetTimestamp();
        try
        {
            await handler.Delegate(envelope, scope.ServiceProvider, ct);
            var elapsed = GetElapsed(start);
            var level = _options.GetLogLevel(envelope, elapsed);
            _logger.Log(level, "Invoked event handler {EventHandler} for event {@Event}. Elapsed: {Elapsed:0.0000} ms", 
                handler.Name, logEvent, elapsed.TotalMilliseconds);
        }
        catch (Exception e)
        {
            var elapsed = GetElapsed(start);
            _logger.LogError(e, "Error processing event handler {EventHandler} for event {@Event}. Elapsed: {Elapsed:0.0000} ms", 
                handler.Name, logEvent, elapsed.TotalMilliseconds);
            if (_options.PropagateEventHandlerExceptions)
                throw;
            //This allows suppressing exceptions if that is desired
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
        if (type.DeclaringType != null)
            return $"{GetTypeName(type.DeclaringType)}+{type.Name}";
        return type.Name;
    }
    
    #endregion
    
    #region Events
    
    private async Task OnEventHandled(EventEnvelope envelope)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var handlers = scope.ServiceProvider
                .GetRequiredService<IEnumerable<IEventBusEvents>>()
                .ToList();
            if (handlers.Count == 0)
                return;
            _logger.LogDebug("Processing OnEventHandled for {Handlers} handler(s)", handlers.Count);
            await Task.WhenAll(handlers.Select(h => h.OnEventHandled(envelope)));
            
            _logger.LogDebug("Processed OnEventHandled events for {Handlers} handler(s)", handlers.Count);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing OnEventHandled events");
            if (_options.PropagateEventHandlerExceptions)
                throw;
        }
    }

    #endregion

    #region Dataflow
    
    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader,
        EventEnvelope messageValue,
        ISourceBlock<EventEnvelope>? source, 
        bool consumeToAccept) =>
        _targetBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    public void Complete()
    {
        _targetBlock.Complete();
    }

    public void Fault(Exception exception)
    {
        _targetBlock.Fault(exception);
    }

    public Task Completion { get; }

    #endregion
}