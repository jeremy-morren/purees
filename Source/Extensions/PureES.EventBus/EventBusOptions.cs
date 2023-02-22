using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PureES.Core;

namespace PureES.EventBus;

public class EventBusOptions
{
    private static readonly Func<EventEnvelope, Exception?, double, LogLevel> _defaultGetLogLevel = 
        (_, e, _) => e != null ? LogLevel.Error :LogLevel.Information;
    
    /// <summary>
    ///     Indicates whether exceptions inside event handlers
    ///     should be rethrown (defaults to <c>false</c>)
    /// </summary>
    /// <remarks>
    ///     When true, this will manifest as an <see cref="AggregateException" />
    ///     on <see cref="IEventBus.Publish" />.
    /// </remarks>
    public bool PropagateEventHandlerExceptions { get; set; } = false;

    /// <summary>
    /// A delegate that takes the event envelope, the exception (if errored) and the elapsed milliseconds
    /// and returns the desired log level
    /// </summary>
    /// <remarks>
    /// By default, <see cref="LogLevel.Information"/> is used for success and <see cref="LogLevel.Error"/> is used for failure
    /// </remarks>
    public Func<EventEnvelope, Exception?, double, LogLevel> GetLogLevel { get; set; } = _defaultGetLogLevel;
}