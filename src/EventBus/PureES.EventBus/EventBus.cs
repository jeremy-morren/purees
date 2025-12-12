using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable ExplicitCallerInfoArgument

// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace PureES.EventBus;

public class EventBus : IEventBus
{
    private readonly ITargetBlock<EventEnvelope> _target;

    private readonly ILogger<EventBus> _logger;
    private readonly IServiceProvider _services;

    public EventBus(
        IServiceProvider services,
        ExecutionDataflowBlockOptions? options = null,
        ILogger<EventBus>? logger = null)
    {
        _services = services;
        _logger = logger ?? NullLogger<EventBus>.Instance;

        var handler = new EventStreamBlock(Handle, options ?? new ExecutionDataflowBlockOptions());

        var beforeHandled = new TransformBlock<EventEnvelope, EventEnvelope>(BeforeEventHandled,
            new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true,
                BoundedCapacity = DataflowBlockOptions.Unbounded, //No backpressure before handle
                MaxDegreeOfParallelism = 1 //Handle 1 at a time
            });
        beforeHandled.LinkTo(handler, new DataflowLinkOptions() {PropagateCompletion = true});

        var onHandled = new ActionBlock<EventEnvelope>(AfterEventHandled,
            new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true,
                BoundedCapacity = DataflowBlockOptions.Unbounded, //No backpressure after handle
                MaxDegreeOfParallelism = 1 //Handle 1 at a time
            });
        handler.LinkTo(onHandled, new DataflowLinkOptions() {PropagateCompletion = true});
        
        Completion = onHandled.Completion;

        _target = beforeHandled;
    }

    #region Handle

    private async Task Handle(EventEnvelope envelope)
    {
        //Start a new root activity for this event
        Activity.Current = null;

        using var activity = PureESTracing.ActivitySource.StartActivity(
            "EventBus.Handle", ActivityKind.Internal, parentContext: default);

        if (activity != null)
        {
            var typeName = GetTypeName(GetEventType(envelope));

            activity.DisplayName = $"EventBus.Handle {typeName}";
            if (activity.IsAllDataRequested)
            {
                activity.SetTag("StreamId", envelope.StreamId);
                activity.SetTag("StreamPosition", envelope.StreamPosition);
                activity.SetTag("EventType", typeName);
            }
        }

        var logEvent = new
        {
            envelope.StreamId, 
            envelope.StreamPosition, 
            EventType = GetEventType(envelope)
        };
        
        try
        {
            await using var scope = _services.CreateAsyncScope();

            var provider = scope.ServiceProvider.GetRequiredService<IEventHandlersProvider>();
            var handlers = provider.GetHandlers(envelope.Event.GetType());
            
            if (handlers.Count == 0) return;
    
            _logger.LogDebug("Processing {EventHandlerCount} event handler(s) for event {@Event}", 
                handlers.Count, logEvent);
            var start = Stopwatch.GetTimestamp();

            foreach (var handler in handlers)
                await handler.Handle(envelope);
            
            var elapsed = GetElapsed(start);
            _logger.LogDebug(
                "Processed {EventHandlerCount} event handler(s) for event {@Event}. Elapsed: {Elapsed:0.0000} ms",
                handlers.Count, logEvent, elapsed.TotalMilliseconds);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.SetTag("error.type", e.GetType().FullName ?? e.GetType().Name);

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

    private static string GetTypeName(Type type) => TypeNameFormatter.GetDisplayTypeName(type);

    #endregion
    
    #region Events

    private async Task<EventEnvelope> BeforeEventHandled(EventEnvelope envelope)
    {
        await InvokeEvent(h => h.BeforeEventHandled(envelope), nameof(IEventBusEvents.BeforeEventHandled));
        return envelope;
    }

    private Task AfterEventHandled(EventEnvelope envelope) =>
        InvokeEvent(h => h.AfterEventHandled(envelope), nameof(IEventBusEvents.AfterEventHandled));

    private async Task InvokeEvent(Func<IEventBusEvents, Task> handle, string methodName)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var handlers = scope.ServiceProvider
                .GetService<IEnumerable<IEventBusEvents>>()
                ?.ToList();
            if (handlers == null || handlers.Count == 0)
                return;

            _logger.LogDebug("Processing {Method} for {Handlers} handler(s)", methodName, handlers.Count);

            await Task.WhenAll(handlers.Select(handle));

            _logger.LogDebug("Processed {Method} for {Handlers} handler(s)", methodName, handlers.Count);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing {Method} events", methodName);
        }
    }

    #endregion

    #region Dataflow
    
    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader,
        EventEnvelope messageValue,
        ISourceBlock<EventEnvelope>? source, 
        bool consumeToAccept) =>
        _target.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    public void Complete()
    {
        _target.Complete();
    }

    public void Fault(Exception exception)
    {
        _target.Fault(exception);
    }

    public Task Completion { get; }

    #endregion
}