using System.ComponentModel;
using PureES.Core.SourceGenerators.Framework;

namespace PureES.Core.SourceGenerators.Generators;

internal class EventHandlerGenerator
{
    private readonly Models.EventHandler _handler;

    private readonly IndentedWriter _w = new();

    private EventHandlerGenerator(Models.EventHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public static string Generate(Models.EventHandler handler, out string filename)
    {
        filename = $"{Namespace}.{GetClassName(handler)}";
        return new EventHandlerGenerator(handler).Generate();
    }
    
    private string Generate()
    {
        _w.WriteFileHeader(false);
    
        _w.WriteLine();
        
        _w.WriteStatement($"namespace {Namespace}", () =>
        {
            _w.WriteLine($"///<summary><c>{_handler.Parent.FullName}.{_handler.Method.Name}</c></summary>");
            
            _w.WriteClassAttributes();
            
            _w.WriteStatement($"internal class {GetClassName(_handler)} : {GetInterface(_handler.EventType)}", () =>
            {
                WriteConstructor();
                
                WriteReflectionProperties();
    
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
    
        for (var i = 0; i < _handler.Services.Length; i++)
            _w.WriteLine($"private readonly {_handler.Services[i].CSharpName} _service{i};");
        
        _w.WriteLine($"private readonly {_handler.Parent.CSharpName} _parent;");
    
        _w.WriteMethodAttributes();
        _w.Write($"public {GetClassName(_handler)}(");
        
        _w.WriteParameters(_handler.Services.Select((s,i) => $"{s.CSharpName} service{i}"),
            new []
            {
                $"{_handler.Parent.CSharpName} parent",
                $"{OptionsType} options",
                $"{LoggerType} logger = null"
            });
        _w.WriteRawLine(')');
        _w.PushBrace();
    
        _w.WriteLine($"this._options = options?.Value.{nameof(PureESOptions.EventHandlers)} ?? throw new ArgumentNullException(nameof(options));");
        _w.WriteLine($"this._logger = logger ?? {NullLoggerInstance};");
    
        for (var i = 0; i < _handler.Services.Length; i++)
            _w.WriteLine($"this._service{i} = service{i} ?? throw new ArgumentNullException(nameof(service{i}));");

        _w.WriteLine("this._parent = parent ?? throw new ArgumentNullException(nameof(parent));");
        
        _w.PopBrace();
    }

    private void WriteReflectionProperties()
    {
        _w.WriteLine($"private static readonly global::{typeof(Type).FullName} ParentType = typeof({_handler.Parent.CSharpName});");

        var type = _handler.EventType == null ? "null" : $"typeof({_handler.EventType.CSharpName})";
        _w.WriteLine($"private static readonly global::{typeof(Type).FullName} EventType = {type};");
        
        var methodInfo = $"global::{typeof(MethodInfo).FullName}";
        
        var flags = $"global::{typeof(BindingFlags).FullName}";
        flags = $"{flags}.{nameof(BindingFlags.Public)} | {flags}.";
        flags += _handler.Method.IsStatic ? $"{nameof(BindingFlags.Static)}" : $"{nameof(BindingFlags.Instance)}";
        
        //Get method include parameter types, to handle methods with multiple overloads
        var parameters = _handler.Method.Parameters.Select(p => $"typeof({p.Type.CSharpName})");

        _w.WriteLine(
            $"private static readonly {methodInfo} _method = ParentType.GetMethod(name: \"{_handler.Method.Name}\", bindingAttr: {flags}, types: new [] {{ {string.Join(", ", parameters)} }});");
        
        _w.WriteLine();

        _w.WriteBrowsableState();
        _w.WriteStatement($"public {methodInfo} {nameof(IEventHandler.Method)}", () =>
        {
            _w.WriteMethodAttributes();
            _w.WriteLine($"get => _method ?? throw new InvalidOperationException($\"Could not locate method '{_handler.Method.Name}' on {{ParentType}}\");");
        });
    }
    
    private void WriteInvoke()
    {
        _w.WriteMethodAttributes();

        var signature = _handler.IsAsync ? "async " : null;
        
        _w.WriteLine($"public {signature}{TaskType} {nameof(IEventHandler<int>.Handle)}({EventEnvelopeType} @event)");
        _w.PushBrace();
        
        if (_handler.EventType != null)
            //Validate correct event input
            _w.WriteStatement($"if (@event.{nameof(EventEnvelope.Event)} is not {_handler.EventType.CSharpName})",
                "throw new ArgumentException(nameof(@event));");
        
        WriteStartActivity();
        
        BeginLogScope(_handler);
        
        WriteHandle();
        
        if (!_handler.IsAsync)
            _w.WriteLine($"return {TaskType}.{nameof(Task.CompletedTask)};");
        
        _w.PopBrace(); //Log scope
        
        _w.PopBrace(); //Activity
        
        _w.PopBrace(); //Method
    }

    private void WriteStartActivity()
    {
        //Because this handler is called without a parent activity (i.e. not from a http request), we provide a parent activity here
        _w.WriteLine($"using (var activity = new {ExternalTypes.Activity}(\"{ActivitySource}\"))");
        
        _w.PushBrace();
        
        _w.WriteLine($"activity.SetTag(\"{nameof(EventEnvelope.StreamId)}\", @event.{nameof(EventEnvelope.StreamId)});");
        _w.WriteLine($"activity.SetTag(\"{nameof(EventEnvelope.StreamPosition)}\", @event.{nameof(EventEnvelope.StreamPosition)});");
        
        var friendlyName = _handler.EventType?.CSharpName.Replace("global::", string.Empty);
        _w.WriteLine(friendlyName != null
            ? $"activity.SetTag(\"EventType\", \"{friendlyName}\");"
            : "activity.SetTag(\"EventType\", null);");
        
        _w.WriteLine($"{ExternalTypes.Activity}.Current = activity;");
        _w.WriteLine("activity.Start();");
    }

    private void WriteHandle()
    {
        _w.WriteLine($"var ct = new CancellationTokenSource(_options.{nameof(PureESEventHandlerOptions.Timeout)}).Token;");
        
        var method = $"\"{_handler.Method.Name}\"";
        
        _w.WriteLine($"var start = {GeneratorHelpers.GetTimestamp};");
        
        _w.WriteStatement("try", () =>
        {
            _w.WriteLogMessage("Debug", 
                "null",
                "Handling event {StreamId}/{StreamPosition}. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                $"@event.{nameof(EventEnvelope.StreamId)}",
                $"@event.{nameof(EventEnvelope.StreamPosition)}",
                "EventType",
                method,
                "ParentType");
            
            var parent = _handler.Method.IsStatic ? _handler.Parent.CSharpName : "this._parent";
            
            _w.Write($"{(_handler.IsAsync ? "await " : null)}{parent}.{_handler.Method.Name}(");
            _w.WriteParameters(_handler.Method.Parameters.Select(p =>
            {
                if (p.HasAttribute<EventAttribute>())
                    return $"({_handler.EventType!.CSharpName})@event.{nameof(EventEnvelope.Event)}";
                if (p.Type.IsGenericEventEnvelope())
                    return $"new {p.Type.CSharpName}(@event)";
                if (p.Type.IsNonGenericEventEnvelope())
                    return "@event";
                if (p.HasFromServicesAttribute())
                    return $"this._service{_handler.Services.GetIndex(p.Type)}";
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
                "EventType",
                method,
                "ParentType");
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
                "EventType",
                method,
                "ParentType");
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
                "EventType",
                method,
                "ParentType");
            _w.WriteStatement($"if ({propagate})", "throw;");
        });
    }

    private void BeginLogScope(Models.EventHandler handler)
    {
        _w.WriteLine($"using (_logger.BeginScope(new {LoggerScopeType}()");
        _w.Push();
        _w.PushBrace();
        var parameters = new []
        {
            ("EventType", "EventType"),
            ("EventHandlerParent", "ParentType"),
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
    
    public static string GetClassName(Models.EventHandler handler)
    {
        var name = handler.EventType != null ? TypeNameHelpers.SanitizeName(handler.EventType) : "CatchAll";
        
        //Append the method name
        var methodName = handler.Parent.FullName.Replace(".", string.Empty);
        methodName += $"_{handler.Method.Name}";
            
        methodName = new[] { '+', '<', '>', '[', ']', '`' }.Aggregate(methodName, (s, c) => s.Replace(c, '_'));
        
        return $"{name}EventHandler_{methodName}";
    }

    public static string GetInterface(IType? eventType)
    {
        return eventType == null 
            ? $"global::{typeof(IEventHandler).FullName}" 
            : TypeNameHelpers.GetGenericTypeName(typeof(IEventHandler<>), eventType.CSharpName);
    }

    private string LoggerType => ExternalTypes.ILogger(GetClassName(_handler));

    private string NullLoggerInstance => ExternalTypes.NullLoggerInstance(GetClassName(_handler));

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