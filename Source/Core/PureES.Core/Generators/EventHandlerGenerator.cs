using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;
using EventHandler = PureES.Core.Generators.Models.EventHandler;

namespace PureES.Core.Generators;

internal class EventHandlerGenerator
{
    private readonly EventHandlerCollection _handlers;

    private readonly IndentedWriter _w = new();

    private EventHandlerGenerator(EventHandlerCollection handlers)
    {
        _handlers = handlers;
    }

    public static string Generate(EventHandlerCollection handlers, out string filename)
    {
        filename = $"{Namespace}.{GetClassName(handlers.EventType)}";
        return new EventHandlerGenerator(handlers).Generate();
    }
    
    private string Generate()
    {
        _w.WriteFileHeader(false);
    
        _w.WriteLine("using System;");
        _w.WriteLine($"using {typeof(ILogger<>).Namespace};");
    
        _w.WriteLine();
        
        _w.WriteStatement($"namespace {Namespace}", () =>
        {
            _w.WriteClassAttributes(EditorBrowsableState.Never);
    
            _w.WriteStatement($"internal class {ClassName} : {Interface}", () =>
            {
                WriteConstructor();
    
                GeneratorHelpers.WriteGetElapsed(_w, true);
                
                WriteInvoke();
            });
        });
        
        return _w.Value;
    }
    
    private void WriteConstructor()
    {
        _w.WriteLine($"private readonly {LoggerType} _logger;");
        _w.WriteLine($"private readonly {EventHandlerOptionsType} _options;");
    
        for (var i = 0; i < _handlers.Services.Count; i++)
            _w.WriteLine($"private readonly {_handlers.Services[i].CSharpName} _service{i};");
        
        for (var i = 0; i < _handlers.Parents.Count; i++)
            _w.WriteLine($"private readonly {_handlers.Parents[i].CSharpName} _parent{i};");
    
        _w.WriteLine();
    
        _w.Write($"public {ClassName}(");
        
        _w.WriteParameters(_handlers.Services.Select((s,i) => $"{s.CSharpName} service{i}"),
            _handlers.Parents.Select((p,i) => $"{p.CSharpName} parent{i}"),
            new []
            {
                $"{OptionsType} options",
                $"{LoggerType} logger = null"
            });
        _w.WriteRawLine(')');
        _w.PushBrace();
    
        _w.WriteLine($"this._options = options?.{nameof(IOptions<object>.Value)}.{nameof(PureESOptions.EventHandlers)} ?? throw new ArgumentNullException(nameof(options));");
        _w.WriteLine($"this._logger = logger ?? {NullLogger};");
    
        for (var i = 0; i < _handlers.Services.Count; i++)
            _w.WriteLine($"this._service{i} = service{i} ?? throw new ArgumentNullException(nameof(service{i}));");

        for (var i = 0; i < _handlers.Parents.Count; i++)
            _w.WriteLine($"this._parent{i} = parent{i} ?? throw new ArgumentNullException(nameof(parent{i}));");
        
        _w.PopBrace();
    }
    
    private void WriteInvoke()
    {
        //If no non-async handlers, method will not be async (see above)
        var isMethodAsync =
            _handlers.Handlers.Any(h => h.Method.ReturnType != null && h.Method.ReturnType.IsAsync(out _));
        var async = isMethodAsync ? "async " : string.Empty;
        
        _w.WriteMethodAttributes();
        _w.WriteLine($"public {async}{typeof(Task).FullName} {nameof(IEventHandler<int>.Handle)}(global::{typeof(EventEnvelope)} @event)");
        _w.PushBrace();
        
        _w.WriteLine($"var ct = new CancellationTokenSource(_options.{nameof(PureESEventHandlerOptions.Timeout)}).Token;");
        
        WriteStartActivity();
        
        foreach (var handler in _handlers.Handlers)
        {
            var method = $"\"{handler.Method.Name}\"";
            _w.WriteLine($"// {handler.Method.Name} on {handler.Parent.FullName}");

            BeginLogScope(handler);
            
            _w.WriteLine($"var start = {GeneratorHelpers.GetTimestamp};");
            
            _w.WriteStatement("try", () =>
            {
                _w.WriteLogMessage("Debug", 
                    "null",
                    "Handling event {@StreamId}/{@StreamPosition}. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    $"@event.{nameof(EventEnvelope.StreamId)}",
                    $"@event.{nameof(EventEnvelope.StreamPosition)}",
                    $"typeof({_handlers.EventType.CSharpName})",
                    method,
                    $"typeof({handler.Parent.CSharpName})");
                var await = handler.Method.ReturnType != null && handler.Method.ReturnType.IsAsync(out _)
                    ? "await "
                    : string.Empty;
                if (handler.Method.IsStatic)
                {
                    _w.Write($"{@await}{handler.Parent.CSharpName}.{handler.Method.Name}(");
                }
                else
                {
                    //use DI parent
                    var index = _handlers.Parents.GetIndex(handler.Parent);
                    _w.Write($"{@await}parent{index}.{handler.Method.Name}(");
                }
                _w.WriteParameters(handler.Method.Parameters.Select(p =>
                {
                    if (p.HasAttribute<EventAttribute>())
                        return $"({handler.Event.CSharpName})@event.{nameof(EventEnvelope.Event)}";
                    if (p.Type.IsGenericEventEnvelope())
                        return $"new {p.Type.CSharpName}(@event)";
                    if (p.HasFromServicesAttribute())
                        return $"service{_handlers.Services.GetIndex(p.Type)}";
                    if (p.Type.IsCancellationToken())
                        return "ct";
                    throw new NotImplementedException("Unknown parameter");
                }));
                _w.WriteRawLine(");");
                _w.WriteLine("var elapsed = GetElapsedTimespan(start);");
                const string getLevel = $"_options.{nameof(PureESEventHandlerOptions.GetLogLevel)}";
                _w.WriteLogMessage($"{getLevel}(elapsed)", 
                    "null",
                    "Handled event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    $"@event.{nameof(EventEnvelope.StreamId)}",
                    $"@event.{nameof(EventEnvelope.StreamPosition)}",
                    $"elapsed.{nameof(TimeSpan.TotalMilliseconds)}",
                    $"typeof({_handlers.EventType.CSharpName})",
                    method,
                    $"typeof({handler.Parent.CSharpName})");
            });
            
            const string propagate = $"_options.{nameof(PureESEventHandlerOptions.PropagateExceptions)}";
            const string logLevel = nameof(LogLevel);
            const string logErrorLevel = $"{propagate} ? {logLevel}.{nameof(LogLevel.Information)} : {logLevel}.{nameof(LogLevel.Error)}";
            
            _w.WriteStatement($"catch (global::{typeof(OperationCanceledException).FullName} ex)", () =>
            {
                _w.WriteLogMessage(logErrorLevel, 
                    "ex",
                    "Timed out while handling event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    $"@event.{nameof(EventEnvelope.StreamId)}",
                    $"@event.{nameof(EventEnvelope.StreamPosition)}",
                    "GetElapsed(start)",
                    $"typeof({_handlers.EventType.CSharpName})",
                    method,
                    $"typeof({handler.Parent.CSharpName})");
                _w.WriteStatement($"if ({propagate})", "throw;");
            });
            _w.WriteStatement("catch (global::System.Exception ex)", () =>
            {
                _w.WriteLogMessage(logErrorLevel, 
                    "ex",
                    "Error handling event {@StreamId}/{@StreamPosition}. Elapsed: {0.0000}ms. Event Type: {@EventType}. Event handler {@EventHandler} on {@EventHandlerParent}",
                    $"@event.{nameof(EventEnvelope.StreamId)}",
                    $"@event.{nameof(EventEnvelope.StreamPosition)}",
                    "GetElapsed(start)",
                    $"typeof({_handlers.EventType.CSharpName})",
                    method,
                    $"typeof({handler.Parent.CSharpName})");
                _w.WriteStatement($"if ({propagate})", "throw;");
            });
            
            _w.PopBrace(); //Log scope
            _w.WriteLine();
        }

        if (!isMethodAsync)
            _w.WriteLine($"return global::{typeof(Task).FullName}.{nameof(Task.CompletedTask)};");
        
        _w.PopBrace(); //Activity
        
        _w.PopBrace();
    }

    private void WriteStartActivity()
    {
        //Because this handler is called without a parent activity (i.e. not from a http request), we provide a parent activity here
        _w.WriteLine($"using (var activity = new {ActivityType}(\"{ActivitySource}\"))");
        
        _w.PushBrace();
        
        _w.WriteLine($"activity.SetTag(\"{nameof(EventEnvelope.StreamId)}\", @event.{nameof(EventEnvelope.StreamId)});");
        _w.WriteLine($"activity.SetTag(\"{nameof(EventEnvelope.StreamPosition)}\", @event.{nameof(EventEnvelope.StreamPosition)});");
        _w.WriteLine($"activity.SetTag(\"EventType\", \"{FriendlyEventTypeName}\");");
        _w.WriteLine($"{ActivityType}.{nameof(Activity.Current)} = activity;");
        _w.WriteLine("activity.Start();");
    }

    private void BeginLogScope(EventHandler handler)
    {
        _w.WriteLine($"using (_logger.BeginScope(new {LoggerScopeType}()");
        _w.Push();
        _w.PushBrace();
        var parameters = new (string, string)[]
        {
            ("EventType", $"typeof({_handlers.EventType.CSharpName})"),
            ("EventHandlerParent", $"typeof({handler.Parent.FullName})"),
            ("EventHandler", $"\"{handler.Method.Name}\""),
            (nameof(EventEnvelope.StreamId), $"@event.{nameof(EventEnvelope.StreamId)}"),
            (nameof(EventEnvelope.StreamPosition), $"@event.{nameof(EventEnvelope.StreamPosition)}"),
        };
        foreach (var (key, value) in parameters)
            _w.WriteLine($"{{ \"{key}\", {value} }}");
        _w.Pop();
        _w.WriteLine("})");
        _w.Pop();
        _w.PushBrace();
    }
    
    #region Helpers

    private string ClassName => GetClassName(_handlers.EventType);
    private string Interface => GetInterface(_handlers.EventType);
    
    public static string GetClassName(IType eventType) => $"{eventType.Name}EventHandler";

    public static string GetInterface(IType eventType) =>
        TypeNameHelpers.GetGenericTypeName(typeof(IEventHandler<>), eventType.CSharpName);
    
    private string LoggerType =>
        TypeNameHelpers.GetGenericTypeName(typeof(ILogger<>), ClassName);

    private string NullLogger =>
        $"{TypeNameHelpers.GetGenericTypeName(typeof(NullLogger<>), ClassName)}.{nameof(NullLogger<int>.Instance)}";

    private static string OptionsType =>
        TypeNameHelpers.GetGenericTypeName(typeof(IOptions<>), $"global::{typeof(PureESOptions).FullName}");
    
    private static string EventHandlerOptionsType => $"global::{typeof(PureESEventHandlerOptions).FullName}";

    private string LoggerScopeType =>
        TypeNameHelpers.GetGenericTypeName(typeof(Dictionary<string, object>), "string", "object");
    
    private static string ActivityType => $"global::{typeof(Activity).FullName}";

    private string FriendlyEventTypeName => _handlers.EventType.CSharpName.Replace("global::", string.Empty);

    public const string Namespace = "PureES.EventHandlers";
    private const string ActivitySource = "PureES.EventHandlers.EventHandler";

    #endregion
}