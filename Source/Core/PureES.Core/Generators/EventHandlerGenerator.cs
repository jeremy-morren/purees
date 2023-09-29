using System.ComponentModel;
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
        if (handlers == null) throw new ArgumentNullException(nameof(handlers));
        if (handlers.Handlers.Count == 0)
            throw new ArgumentException(nameof(handlers));
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
        _w.WriteLine($"using {ExternalTypes.LoggingNamespace};");
    
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
    
        _w.WriteLine($"this._options = options?.Value.{nameof(PureESOptions.EventHandlers)} ?? throw new ArgumentNullException(nameof(options));");
        _w.WriteLine($"this._logger = logger ?? {NullLoggerInstance};");
    
        for (var i = 0; i < _handlers.Services.Count; i++)
            _w.WriteLine($"this._service{i} = service{i} ?? throw new ArgumentNullException(nameof(service{i}));");

        for (var i = 0; i < _handlers.Parents.Count; i++)
            _w.WriteLine($"this._parent{i} = parent{i} ?? throw new ArgumentNullException(nameof(parent{i}));");
        
        _w.PopBrace();
    }
    
    private void WriteInvoke()
    {
        _w.WriteMethodAttributes();

        var isAsync = _handlers.Handlers.Count > 1 || _handlers.Handlers[0].IsAsync;
        var signature = isAsync ? "async " : null;
        
        _w.WriteLine($"public {signature}{TaskType} {nameof(IEventHandler<int>.Handle)}({EventEnvelopeType} @event)");
        _w.PushBrace();
        
        if (_handlers.EventType != null)
            //Validate correct event input
            _w.WriteStatement($"if (@event.{nameof(EventEnvelope.Event)} is not {_handlers.EventType.CSharpName})",
                "throw new ArgumentException(nameof(@event));");
        
        WriteStartActivity();

        if (_handlers.Handlers.Count > 1)
        {
            //Create an array of tasks
            //Then await Task.WhenAll
        
            _w.WriteLine($"var tasks = new {TaskType}[{_handlers.Handlers.Count}];");

            for (var i = 0; i < _handlers.Handlers.Count; i++)
                _w.WriteLine($"tasks[{i}] = Task.Run(() => {_handlers.Handlers[i].HandlerMethodName}(@event));");
            
            _w.WriteLine($"await {TaskType}.{nameof(Task.WhenAll)}(tasks);");
        }
        else
        {
            //handle the task directly
            var handler = _handlers.Handlers[0];
            if (handler.IsAsync)
            {
                _w.WriteLine($"await {handler.HandlerMethodName}(@event);");
            }
            else
            {
                //Synchronous
                _w.WriteLine($"{handler.HandlerMethodName}(@event);");
                _w.WriteLine($"return {TaskType}.{nameof(Task.CompletedTask)};");
            }
        }

        _w.PopBrace(); //Activity
        
        _w.PopBrace(); //Method

        foreach (var handler in _handlers.Handlers)
            WriteHandlerMethod(handler);
    }

    private void WriteStartActivity()
    {
        //Because this handler is called without a parent activity (i.e. not from a http request), we provide a parent activity here
        _w.WriteLine($"using (var activity = new {ExternalTypes.Activity}(\"{ActivitySource}\"))");
        
        _w.PushBrace();
        
        _w.WriteLine($"activity.SetTag(\"{nameof(EventEnvelope.StreamId)}\", @event.{nameof(EventEnvelope.StreamId)});");
        _w.WriteLine($"activity.SetTag(\"{nameof(EventEnvelope.StreamPosition)}\", @event.{nameof(EventEnvelope.StreamPosition)});");
        
        var friendlyName = _handlers.EventType?.CSharpName.Replace("global::", string.Empty);
        _w.WriteLine(friendlyName != null
            ? $"activity.SetTag(\"EventType\", \"{friendlyName}\");"
            : "activity.SetTag(\"EventType\", null);");
        
        _w.WriteLine($"{ExternalTypes.Activity}.Current = activity;");
        _w.WriteLine("activity.Start();");
    }

    private void WriteHandlerMethod(EventHandler handler)
    {
        _w.WriteMethodAttributes();
        _w.WriteLine($"// {handler.Method.Name} on {handler.Parent.FullName}");
        
        _w.WriteLine($"private {(handler.IsAsync ? $"async {TaskType}" : "void")} {handler.HandlerMethodName}({EventEnvelopeType} @event)");
        _w.PushBrace();
        
        _w.WriteLine($"var ct = new CancellationTokenSource(_options.{nameof(PureESEventHandlerOptions.Timeout)}).Token;");
        _w.WriteLine($"var parentType = typeof({handler.Parent.CSharpName});");
        _w.WriteLine(handler.Event != null
            ? $"var eventType = typeof({handler.Event.CSharpName});"
            : $"global::{typeof(Type).FullName} eventType = null;");
        
        BeginLogScope(handler);
        
        var method = $"\"{handler.Method.Name}\"";
        
        _w.WriteLine($"var start = {GeneratorHelpers.GetTimestamp};");
        
        _w.WriteStatement("try", () =>
        {
            _w.WriteLogMessage("Debug", 
                "null",
                "Handling event {StreamId}/{StreamPosition}. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                $"@event.{nameof(EventEnvelope.StreamId)}",
                $"@event.{nameof(EventEnvelope.StreamPosition)}",
                "eventType",
                method,
                "parentType");
            var parent = handler.Method.IsStatic
                ? handler.Parent.CSharpName
                : $"this._parent{_handlers.Parents.GetIndex(handler.Parent)}";
            _w.Write($"{(handler.IsAsync ? "await " : null)}{parent}.{handler.Method.Name}(");
            _w.WriteParameters(handler.Method.Parameters.Select(p =>
            {
                if (p.HasAttribute<EventAttribute>())
                    return $"({handler.Event!.CSharpName})@event.{nameof(EventEnvelope.Event)}";
                if (p.Type.IsGenericEventEnvelope())
                    return $"new {p.Type.CSharpName}(@event)";
                if (p.Type.IsNonGenericEventEnvelope())
                    return "@event";
                if (p.HasFromServicesAttribute())
                    return $"this._service{_handlers.Services.GetIndex(p.Type)}";
                if (p.Type.IsCancellationToken())
                    return "ct";
                throw new NotImplementedException("Unknown parameter");
            }));
            _w.WriteRawLine(");");
            _w.WriteLine("var elapsed = GetElapsedTimespan(start);");
            _w.WriteLogMessage("this._options.GetLogLevel(@event, elapsed)", 
                "null",
                "Handled event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                $"@event.{nameof(EventEnvelope.StreamId)}",
                $"@event.{nameof(EventEnvelope.StreamPosition)}",
                $"elapsed.{nameof(TimeSpan.TotalMilliseconds)}",
                "eventType",
                method,
                "parentType");
        });
        
        const string propagate = $"_options.{nameof(PureESEventHandlerOptions.PropagateExceptions)}";
        const string logErrorLevel = $"{propagate} ? LogLevel.Information : LogLevel.Error";
        
        _w.WriteStatement($"catch (global::{typeof(OperationCanceledException).FullName} ex)", () =>
        {
            _w.WriteLogMessage(logErrorLevel, 
                "ex",
                "Timed out while handling event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                $"@event.{nameof(EventEnvelope.StreamId)}",
                $"@event.{nameof(EventEnvelope.StreamPosition)}",
                "GetElapsed(start)",
                "eventType",
                method,
                "parentType");
            _w.WriteStatement($"if ({propagate})", "throw;");
        });
        _w.WriteStatement("catch (global::System.Exception ex)", () =>
        {
            _w.WriteLogMessage(logErrorLevel, 
                "ex",
                "Error handling event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                $"@event.{nameof(EventEnvelope.StreamId)}",
                $"@event.{nameof(EventEnvelope.StreamPosition)}",
                "GetElapsed(start)",
                "eventType",
                method,
                "parentType");
            _w.WriteStatement($"if ({propagate})", "throw;");
        });
        
        _w.PopBrace(); //Log scope
        
        _w.PopBrace(); //Method
    }

    private void BeginLogScope(EventHandler handler)
    {
        _w.WriteLine($"using (_logger.BeginScope(new {LoggerScopeType}()");
        _w.Push();
        _w.PushBrace();
        var parameters = new []
        {
            ("EventType", "eventType"),
            ("EventHandlerParent", "parentType"),
            ("EventHandler", $"\"{handler.Method.Name}\""),
            (nameof(EventEnvelope.StreamId), $"@event.{nameof(EventEnvelope.StreamId)}"),
            (nameof(EventEnvelope.StreamPosition), $"@event.{nameof(EventEnvelope.StreamPosition)}"),
        };
        foreach (var (key, value) in parameters)
            _w.WriteLine($"{{ \"{key}\", {value} }},");
        _w.Pop();
        _w.WriteLine("}))");
        _w.Pop();
        _w.PushBrace();
    }
    
    #region Helpers

    private string ClassName => GetClassName(_handlers.EventType);
    private string Interface => GetInterface(_handlers.EventType);
    
    public static string GetClassName(IType? eventType) => $"{eventType?.Name ?? "CatchAll"}EventHandler";

    public static string GetInterface(IType? eventType)
    {
        return eventType == null 
            ? $"global::{typeof(IEventHandler).FullName}" 
            : TypeNameHelpers.GetGenericTypeName(typeof(IEventHandler<>), eventType.CSharpName);
    }

    private string LoggerType => ExternalTypes.ILogger(ClassName);

    private string NullLoggerInstance => ExternalTypes.NullLoggerInstance(ClassName);

    private static string OptionsType => ExternalTypes.IOptions($"global::{typeof(PureESOptions).FullName}");
    
    private static string EventHandlerOptionsType => $"global::{typeof(PureESEventHandlerOptions).FullName}";
    
    private static string TaskType => $"global::{typeof(Task).FullName}";
    private static string EventEnvelopeType => $"global::{typeof(EventEnvelope).FullName}";

    private static string LoggerScopeType =>
        TypeNameHelpers.GetGenericTypeName(typeof(Dictionary<string, object>), "string", "object");

    public const string Namespace = "PureES.EventHandlers";
    private const string ActivitySource = "PureES.EventHandlers.EventHandler";

    #endregion
}