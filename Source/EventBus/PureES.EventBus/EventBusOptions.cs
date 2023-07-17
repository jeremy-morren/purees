using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using PureES.Core;

// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace PureES.EventBus;


public class EventBusOptions
{
    private static readonly Func<EventEnvelope, TimeSpan, LogLevel> DefaultGetLogLevel = 
        (_, elapsed) => elapsed.TotalSeconds > 30 ? LogLevel.Warning : LogLevel.Information;

    /// <summary>
    /// Gets or sets the maximum length that event handlers should run before timing out.
    /// </summary>
    /// <remarks>Must be greater than 0. The default is 60 seconds.</remarks>
    public TimeSpan EventHandlerTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Indicates whether exceptions inside event handlers
    ///     should be rethrown (defaults to <c>false</c>)
    /// </summary>
    /// <remarks>
    ///     When true, this will cause <see cref="IEventBus"/> to fault and stop accepting new messages
    ///     (i.e. <see cref="IEventBus.Completion"/> will complete in a faulted state)
    /// </remarks>
    public bool PropagateEventHandlerExceptions { get; set; } = false;

    /// <summary>
    /// A delegate that takes the event envelope and the elapsed milliseconds
    /// and returns the desired log level
    /// </summary>
    /// <remarks>
    /// By default, <see cref="LogLevel.Warning" /> is returned if the handler took longer than 1 minute,
    /// otherwise <see cref="LogLevel.Debug"/>
    /// </remarks>
    public Func<EventEnvelope, TimeSpan, LogLevel> GetLogLevel { get; set; } = DefaultGetLogLevel;
    
    /// <summary>
    ///     Gets or sets the maximum number of event streams that may be processed simultaneously.
    ///     The default is <c>-1</c> (no limit)
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = DataflowBlockOptions.Unbounded;

    /// <summary>
    ///     Gets or sets the maximum number of events that may be buffered by <see cref="IEventBus"/>.
    ///     The default is <c>1</c>
    /// </summary>
    public int BufferSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets a delegate that will be called after an event is handled (i.e. all event handlers are invoked)
    /// </summary>
    public Func<EventEnvelope, IServiceProvider, Task>? OnEventHandled { get; set; }
}