using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace PureES.EventStores.Tests.Logging;

public class TestOutputHelperSink : ILogEventSink
{
    public const string OutputTemplate = "{SourceContext,-50} [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
    
    private readonly ITestOutputHelper? _output;
    private readonly MessageTemplateTextFormatter _formatter;

    public TestOutputHelperSink(ITestOutputHelper? output, 
        string outputTemplate = OutputTemplate,
        IFormatProvider? formatProvider = null)
    {
        _output = output;
        _formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
    }

    public void Emit(LogEvent logEvent)
    {
        if (_output == null) return;
        var sb = new StringWriter();
        _formatter.Format(logEvent, sb);
        var str = sb.ToString();
        //Remove trailing newline
        // if (str.EndsWith(Environment.NewLine))
        //     str = str[..^Environment.NewLine.Length];
        _output.WriteLine(str);
    }
}
