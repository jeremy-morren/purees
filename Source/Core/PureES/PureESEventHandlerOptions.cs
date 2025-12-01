using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace PureES;

[PublicAPI]
public class PureESEventHandlerOptions
{
    /// <summary>
    ///     Indicates whether exceptions inside event handlers
    ///     should be rethrown (defaults to <c>false</c>)
    /// </summary>
    /// <remarks>
    ///     When true, this will cause <see cref="IEventHandler"/> to throw any exceptions.
    ///     When false, exceptions will be caught and logged.
    /// </remarks>
    public bool PropagateExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum time that event handlers should run before timing out.
    /// </summary>
    /// <remarks>Must be greater than 0. The default is 5 minutes.</remarks>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum time in seconds that event handlers should run before timing out.
    /// </summary>
    /// <remarks>Must be greater than 0. The default is 300 seconds (5 minutes)</remarks>
    public double TimeoutSeconds
    {
        get => Timeout.TotalSeconds;
        set => Timeout = TimeSpan.FromSeconds(value);
    }

    /// <summary>
    /// Retry policy to use for synchronous event handlers
    /// </summary>
    public IRetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// Retry policy to use for asynchronous event handlers
    /// </summary>
    public IAsyncPolicy? AsyncRetryPolicy { get; set; }

    /// <summary>
    /// A delegate that takes the event envelope and the elapsed milliseconds
    /// and returns the desired log level
    /// </summary>
    /// <remarks>
    /// By default, <see cref="LogLevel.Warning" /> is returned if the handler took longer than 1 minute,
    /// otherwise <see cref="LogLevel.Debug"/>
    /// </remarks>
    public Func<EventEnvelope, TimeSpan, LogLevel> GetLogLevel { get; set; } = DefaultGetLogLevel;

    private static LogLevel DefaultGetLogLevel(EventEnvelope e, TimeSpan ts) =>
        ts.TotalSeconds > 5 ? LogLevel.Warning : LogLevel.Information;

    internal bool Validate()
    {
        if (GetLogLevel == null!)
            throw new Exception($"{nameof(GetLogLevel)} is required");
        if (Timeout.Ticks <= 0)
            throw new Exception($"{nameof(Timeout)} must be greater than 0");
        return true;
    }
}