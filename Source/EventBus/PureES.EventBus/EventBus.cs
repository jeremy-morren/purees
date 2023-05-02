using System.Diagnostics;
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
        
        _targetBlock = new EventStreamBlock(Publish, _options);
    }

    #region Handle

    private async Task Publish(EventEnvelope envelope)
    {
        var eventType = GetEventType(envelope);
        foreach (var handler in _eventHandlers.GetEventHandlers(eventType))
        {
            //TODO: Annotate the log events with the event handler method name
            var logProps = new
            {
                envelope.StreamId, 
                envelope.StreamPosition, 
                EventType = GetEventType(envelope)
            };
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                {"EventHandlerEventType", GetEventType(envelope)},
                {"EventHandlerStreamId", envelope.StreamId},
                {"EventHandlerStreamPosition", envelope.StreamPosition}
            });

            var ct = new CancellationTokenSource(_options.EventHandlerTimeout).Token;
                
            await using var scope = _services.CreateAsyncScope();
            var start = Stopwatch.GetTimestamp();
            try
            {
                _logger.LogDebug("Handling projection for event {@Event}", logProps);
                await handler(envelope, scope.ServiceProvider, ct);
                var elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                var level = _options.GetLogLevel(envelope, null, elapsedMs);
                _logger.Log(level,
                    "Successfully handled projection for event {@Event}. Elapsed: {Elapsed:0.0000} ms", 
                    logProps, elapsedMs);
            }
            catch (Exception e)
            {
                var elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                var level = _options.GetLogLevel(envelope, e, elapsedMs);
                _logger.Log(level, 
                    e, 
                    "Error processing projection for event {@Event}. Elapsed: {Elapsed:0.0000} ms",
                    logProps, elapsedMs);
                if (_options.PropagateEventHandlerExceptions)
                    throw;
                //This allows suppressing exceptions if that is desired
            }
        }
    }

    private static double GetElapsedMilliseconds(long start, long stop) =>
        (stop - start) * 1000 / (double) Stopwatch.Frequency;
    
    private static Type GetEventType(EventEnvelope envelope) => 
        envelope.Event?.GetType() ?? throw new InvalidOperationException("Event is null");
    
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

    public Task Completion => _targetBlock.Completion;
    
    #endregion
}