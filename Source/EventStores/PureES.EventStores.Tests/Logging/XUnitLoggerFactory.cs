using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace PureES.EventStores.Tests.Logging;


public sealed class XUnitLoggerFactory : ILoggerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public XUnitLoggerFactory(ITestOutputHelper? output,
        LogEventLevel minimumLevel = LogEventLevel.Warning,
        string outputTemplate = TestOutputHelperSink.OutputTemplate,
        IFormatProvider? formatProvider = null)
    {
        var sink = new TestOutputHelperSink(output, outputTemplate, formatProvider);
        _loggerFactory = new SerilogLoggerFactory(new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .MinimumLevel.Is(minimumLevel)
            .CreateLogger());
    }

    public void Dispose() => _loggerFactory.Dispose();

    public ILogger CreateLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);

    public void AddProvider(ILoggerProvider provider) => _loggerFactory.AddProvider(provider);
}