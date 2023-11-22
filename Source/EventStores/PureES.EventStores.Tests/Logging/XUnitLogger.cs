using Microsoft.Extensions.Logging;
using Serilog.Events;
using Xunit.Abstractions;

namespace PureES.EventStores.Tests.Logging;

public class XUnitLogger<TCategory> : ILogger<TCategory>
{
    private readonly ILogger<TCategory> _logger;

    public XUnitLogger(ITestOutputHelper outputHelper, 
        LogEventLevel minimumLevel = LogEventLevel.Debug,
        string outputTemplate = TestOutputHelperSink.OutputTemplate,
        IFormatProvider? formatProvider = null) =>
        _logger = new XUnitLoggerFactory(outputHelper, minimumLevel, outputTemplate, formatProvider)
            .CreateLogger<TCategory>();

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }

    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

#if NET6_0_OR_GREATER
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);
    
#else
    
    public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);

#endif
}