using System.Diagnostics;
using JetBrains.Annotations;
using PureES.Core.Generators.Aggregates.Models;
using PureES.Core.Generators.Framework;
// ReSharper disable StringLiteralTypo

namespace PureES.Core.Generators.Aggregates;

internal class CommandHandlerGenerator
{

    public static string Generate(Aggregate aggregate, Handler handler)
    {
        var generator = new CommandHandlerGenerator();
        if (handler.ResultType == null) return generator.GenerateNoResult(aggregate, handler);
        throw new NotImplementedException();
    }

    private readonly IndentedWriter _w = new();

    private string GenerateNoResult(Aggregate aggregate, Handler handler)
    {
        _w.WriteFileHeader(true);

        _w.WriteLine("using System.Threading.Tasks;"); //Enable WithCancellation extension method
        _w.WriteLine("using Microsoft.Extensions.Logging;"); //Enable log extension methods
        _w.WriteLine("using System.Diagnostics;"); //Enable log extension methods

        _w.WriteLine();
        
        _w.WriteLine("namespace PureES.CommandHandlers");
        _w.WriteLineThenPush('{');
        
        _w.WriteClassAttributes();
        _w.WriteLine($"internal class {handler.Command.Name}Handler : ICommandHandler<{handler.Command.CSharpName}>");
        _w.WriteLineThenPush('{');

        WriteConstructor(aggregate, handler);

        _w.WriteLine();
        
        WriteHelperMethods();

        _w.WriteLine();

        _w.WriteDebuggerAttributes();
        _w.WriteLine(
            $"public async Task<ulong> Handle({handler.Command.CSharpName} command, CancellationToken cancellationToken)");
        _w.WriteLineThenPush('{');

        WriteHandle(aggregate, handler);
        
        _w.PopAll();

        return _w.Value;
    }

    private void WriteConstructor(Aggregate aggregate, Handler handler)
    {
        //Write service definitions
        
        _w.WriteLine($"private readonly {GetStoreType(aggregate)} _store;");
        _w.WriteLine($"private readonly {EnumerableEventEnricherType}? _enrichers;");
        _w.WriteLine($"private readonly {GetEnumerableValidatorType(handler)}? _syncValidators;");
        _w.WriteLine($"private readonly {GetEnumerableAsyncValidatorType(handler)}? _asyncValidators;");
        _w.WriteLine($"private readonly {GetLoggerType(handler)}? _logger;");

        for (var i = 0; i < handler.Services.Length; i++)
            _w.WriteLine($"private readonly {handler.Services[i].CSharpName} _service{i};");
        
        _w.WriteLine();
        
        //Generate constructor for DI
        _w.WriteDebuggerAttributes();
        _w.Write($"public CommandHandler_{handler.Command.Name}(");
        
        //NB: These are last, so we can provide default values of null
        WriteParameters(
            handler.Services.Select((svc, i) => $"{svc.CSharpName} service{i}"),
            new[]
            {
                $"{GetStoreType(aggregate)} store",
                $"{EnumerableEventEnricherType}? enrichers = null",
                $"{GetEnumerableValidatorType(handler)}? syncValidators = null",
                $"{GetEnumerableAsyncValidatorType(handler)}? asyncValidators = null",
                $"{GetLoggerType(handler)}? logger = null"
            });

        _w.WriteRawLine(')');
        _w.WriteLineThenPush('{');
        _w.WriteLine("this._store = store ?? throw new ArgumentNullException(\"store\");");
        _w.WriteLine("this._enrichers = enrichers;");
        _w.WriteLine("this._syncValidators = syncValidators;");
        _w.WriteLine("this._asyncValidators = asyncValidators;");
        _w.WriteLine("this._logger = logger;");
        for (var i = 0; i < handler.Services.Length; i++)
            _w.WriteLine($"this._service{i} = service{i} ?? throw new ArgumentNullException(\"service{i}\");");
        _w.PopThenWriteLine('}');
        
        _w.WriteLine();
    }

    private void WriteHelperMethods()
    {
        //Method to get elapsed time
        var sw = $"global::{typeof(Stopwatch).FullName}";
        var ts = $"global::{typeof(TimeSpan).FullName}";
        _w.WriteLine($"private static readonly double s_tickFrequency = (double){ts}.{nameof(TimeSpan.TicksPerSecond)} / {sw}.Frequency;");
        _w.WriteLine();
        _w.WriteDebuggerAttributes();
        _w.WriteStatement("private double GetElapsed(long start)",
            $"new {ts}((long)(({GetTimestamp} - start) * s_tickFrequency)).{nameof(TimeSpan.TotalMilliseconds)};");
    }

    private void WriteHandle(Aggregate aggregate, Handler handler)
    {
        var cmdType = $"typeof({aggregate.Type.CSharpName})";
        var aggType = $"typeof({handler.Command.CSharpName})";
        var method = $"\"{handler.Method.Name}\"";
        
        WriteLogMessage("Debug",
            "null",
            "Handling command {@Command}. Aggregate: {@Aggregate}. Method: {@Method}", 
            cmdType, aggType, method);

        _w.WriteLine($"var start = {GetTimestamp};");
        
        _w.WriteStatement("try", () =>
        {
            WriteValidate();
            
            if (handler.Method.IsStatic)
            {
                WriteCreateWhen(aggregate, handler);
            }
            else
            {
                throw new NotImplementedException();
            }

            var isEnumerable = WriteCreateEventsList(handler);
            WriteEnrich(isEnumerable);
            
            //TODO: save to event store

            WriteLogMessage("Information",
                "null",
                "Handled command {@Command}. Aggregate: {@Aggregate}. Method: {@Method}. Elapsed: {0.0000}ms",
                cmdType, aggType, method, "GetElapsed(start)");
        });
        _w.WriteStatement($"catch (global::{typeof(Exception)} ex)", () =>
        {
            WriteLogMessage("Error",
                "ex",
                "Error handling command {@Command}. Aggregate: {@Aggregate}. Method: {@Method}. Elapsed: {0.0000}ms",
                cmdType, aggType, method, "GetElapsed(start)");
            _w.WriteLine("throw;");
        });
    }

    private void WriteValidate()
    {
        _w.WriteStatement("if (this._syncValidators != null)",
            () =>
            {
                _w.WriteStatement("foreach (var validator in this._syncValidators)",
                    "validator.Validate(command);");
            });
        _w.WriteStatement("if (this._asyncValidators != null)",
            () =>
            {
                _w.WriteStatement("foreach (var validator in this._asyncValidators)",
                    "await validator.Validate(command, cancellationToken);");
            });
    }

    private void WriteCreateWhen(Aggregate aggregate, Handler handler)
    {
        //Static method
        
        var await = handler.IsAsync ? "await" : null;
        _w.Write($"var result = {@await} {aggregate.Type.CSharpName}.{handler.Method.Name}(");

        var parameters = handler.Method.Parameters
            .Select(p =>
            {
                if (p.HasAttribute<CommandAttribute>())
                    return "command";
                if (p.HasFromServicesAttribute())
                    return $"this._service{handler.Services.IndexOf(p.Type)}";
                if (p.Type.IsCancellationToken())
                    return "cancellationToken";
                throw new NotImplementedException(); //Unknown parameter
            });

        WriteParameters(parameters);

        _w.WriteRawLine(");");
    }

    private bool WriteCreateEventsList(Handler handler)
    {
        var type = $"global::{typeof(UncommittedEvent).FullName}";
        var list = $"new List<{type}>();";
        var newEvent = $"new {type}() {{ Event = e }});";
        
        //local var 'result' is the source
        var source = handler.ResultType != null ? "result.Event" : "result";
        if (handler.EventType.IsEnumerable())
        {
            _w.WriteLine(list);
            _w.WriteStatement($"foreach (var e in {source})", 
                $"events.Add({newEvent});");
            return true;
        }
        else if (handler.EventType.IsAsyncEnumerable())
        {
            _w.WriteLine(list);
            _w.WriteStatement($"await foreach (var e in {source}.WithCancellation(cancellationToken))",
                $"events.Add({newEvent});");
            return true;
        }
        _w.WriteLine($"var e = new {type}() {{ Event = {source} }});");
        return false;
    }

    private void WriteEnrich(bool isEnumerable)
    {
        //IF enumerable, local variable 'events'
        //Otherwise, local variable 'e'
        void Enrich()
        {
            if (isEnumerable)
                _w.WriteStatement("foreach (var e in events)", "enricher.Enrich(e);");
            else
                _w.WriteLine("enricher.Enrich(e);");
        }
        
        _w.WriteStatement("if (this._enrichers != null)",
            () => _w.WriteStatement("foreach (var enricher in this._enrichers)", Enrich));
        
        _w.WriteStatement("if (this._asyncEnrichers != null)",
            () => _w.WriteStatement("foreach (var enricher in this._async Enrichers)", Enrich));
    }
    
    private void WriteLogMessage(string level, string exception, [StructuredMessageTemplate] string message, params string[] args)
    {
        _w.Write($"this._logger?.Log(");
        //LogLevel logLevel, Exception? exception, string? message, params object?[] args)
        WriteParameters(new[]
        {
            $"logLevel: LogEventLevel.{level}",
            $"exception: {exception}",
            $"message: \"{message}\"",
        }, args);

        _w.WriteRawLine(");");
    }
    
    private void WriteParameters(params IEnumerable<string>[] argLists)
    {
        WriteParameters(argLists.SelectMany(l => l).ToArray());
    }
    
    private void WriteParameters(params string[] args)
    {
        var separator = $",\n{_w.GetIndent(1)}";
        _w.WriteRaw(string.Join(separator, args));
    }

    #region Types

    private static string GetTimestamp => $"global::{typeof(Stopwatch).FullName}.{nameof(Stopwatch.GetTimestamp)}()";

    private static string GetLoggerType(Handler handler) => 
        $"global::Microsoft.Extensions.Logging.ILogger<{handler.Command.Name}Handler>";

    private static string GetStoreType(Aggregate aggregate) =>
        GetGenericType(typeof(IAggregateStore<>), aggregate.Type.FullName);

    private static string EnumerableEventEnricherType => 
        GetGenericType(typeof(IEnumerable<>), $"global::{typeof(IEventEnricher).FullName}");

    private static string GetEnumerableValidatorType(Handler handler) =>
        GetGenericType(typeof(IEnumerable<>),
            GetGenericType(typeof(ICommandValidator<>), handler.Command.FullName));

    private static string GetEnumerableAsyncValidatorType(Handler handler) =>
        GetGenericType(typeof(IEnumerable<>),
            GetGenericType(typeof(IAsyncCommandValidator<>), handler.Command.FullName));

    private static string GetGenericType(Type type, string genericArgument)
    {
        var name = type.FullName!;
        var index = name.IndexOf("`", StringComparison.Ordinal);
        return $"global::{name.Substring(0, index)}<{genericArgument}>";
    }
    
    #endregion
}