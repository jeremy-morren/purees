﻿using Microsoft.Extensions.Logging;

namespace PureES.Core;

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
    /// <remarks>Must be greater than 0. The default is 60 seconds.</remarks>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the maximum time in seconds that event handlers should run before timing out.
    /// </summary>
    /// <remarks>Must be greater than 0. The default is 60 seconds.</remarks>
    public double TimeoutSeconds
    {
        get => Timeout.TotalSeconds;
        set => Timeout = TimeSpan.FromSeconds(value);
    }

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

    internal void Validate()
    {
        if (GetLogLevel == null!)
            throw new Exception($"{nameof(GetLogLevel)} is required");
        if (Timeout.Ticks <= 0)
            throw new Exception($"{nameof(Timeout)} must be greater than 0");
    }

}