using PureES.SourceGenerators.Framework;
using EventHandler = PureES.SourceGenerators.Models.EventHandler;

namespace PureES.SourceGenerators;

internal class EventHandlerGenerator
{
    private readonly EventHandler _handler;

    private readonly IndentedWriter _w = new();

    private EventHandlerGenerator(EventHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public static string Generate(EventHandler handler, out string filename)
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

            var interfaces = string.Join(", ", GetInterfaces(_handler));
            
            _w.WriteStatement($"internal class {GetClassName(_handler)} : {interfaces}", () =>
            {
                WriteConstructor();
                
                WriteReflectionProperties();
    
                GeneratorHelpers.WriteGetElapsed(_w, true);
                
                WriteInvoke();
                WriteCanHandle();
            });
        });
        
        return _w.Value;
    }

    private void WriteConstructor()
    {
        _w.WriteLine($"private readonly {LoggerType} _logger;");
        _w.WriteLine($"private readonly global::{PureESSymbols.EventHandlerOptions} _options;");
    
        for (var i = 0; i < _handler.Services.Length; i++)
            _w.WriteLine($"private readonly {_handler.Services[i].CSharpName} _service{i};");
        
        _w.WriteLine($"private readonly {_handler.Parent.CSharpName} _parent;");
    
        _w.WriteMethodAttributes();
        _w.Write($"public {GetClassName(_handler)}(");
        
        _w.WriteParameters(
            _handler.Services.Select((s,i) => $"{s.CSharpName} service{i}"),
            [
                $"{_handler.Parent.CSharpName} parent",
                $"{OptionsType} options",
                $"{LoggerType} logger = null"
            ]);
        _w.WriteRawLine(')');
        _w.PushBrace();
    
        _w.WriteLine("this._options = options?.Value.EventHandlers ?? throw new ArgumentNullException(nameof(options));");
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
        _w.WriteStatement($"public {methodInfo} Method", () =>
        {
            _w.WriteMethodAttributes();
            _w.WriteLine($"get => _method ?? throw new InvalidOperationException($\"Could not locate method '{_handler.Method.Name}' on {{ParentType}}\");");
        });

        _w.WriteLine();
        
        _w.WriteBrowsableState();
        _w.WriteStatement("public int Priority", () =>
        {
            _w.WriteMethodAttributes();
            _w.WriteLine($"get => {_handler.Priority};");
        });
    }
    
    private void WriteInvoke()
    {
        _w.WriteMethodAttributes();

        var signature = _handler.IsAsync ? "async " : null;
        
        _w.WriteLine($"public {signature}Task Handle(global::{PureESSymbols.EventEnvelope} @event)");
        _w.PushBrace();
        
        if (_handler.EventType != null)
            //Validate correct event input
            _w.WriteStatement($"if (@event.Event is not {_handler.EventType.CSharpName})",
                "throw new ArgumentOutOfRangeException($\"Unknown event type {@event.Event.GetType()}\", nameof(@event));");
        
        StartActivity();
        
        BeginLogScope();
        
        WriteHandle();
        
        if (!_handler.IsAsync)
            _w.WriteLine($"return Task.{nameof(Task.CompletedTask)};");
        
        _w.PopBrace(); //Log scope

        _w.PopBrace(); //Activity
        
        _w.PopBrace(); //Method
    }
    
    private void WriteHandle()
    {
        const string getEventType = "@event.Event.GetType()";
        _w.WriteLine("var ct = new CancellationTokenSource(_options.Timeout).Token;");

        var method = _handler.Method.Name.ToStringLiteral();
        
        _w.WriteLine($"var start = {GeneratorHelpers.GetTimestamp};");
        
        _w.WriteStatement("try", () =>
        {
            _w.WriteLogMessage("Debug", 
                "null",
                "Handling event {StreamId}/{StreamPosition}. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                "@event.StreamId", "@event.StreamPosition", getEventType, method, "ParentType");
            
            var parent = _handler.Method.IsStatic ? _handler.Parent.CSharpName : "this._parent";
            
            _w.Write($"{(_handler.IsAsync ? "await " : null)}{parent}.{_handler.Method.Name}(");
            _w.WriteParameters(_handler.Method.Parameters.Select(p =>
            {
                if (p.HasEventAttribute())
                    return $"({_handler.EventType!.CSharpName})@event.Event";
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
                "@event.StreamId", 
                "@event.StreamPosition",
                $"elapsed.{nameof(TimeSpan.TotalMilliseconds)}",
                getEventType,
                method,
                "ParentType");
        });

        const string propagate = "_options.PropagateExceptions";
        const string logErrorLevel = $"{propagate} ? LogLevel.Information : LogLevel.Error";

        _w.WriteStatement("catch (global::System.Exception ex)", () =>
        {
            _w.WriteLogMessage(logErrorLevel,
                "ex",
                "Error handling event {StreamId}/{StreamPosition}. Elapsed: {Elapsed:0.0000}ms. Event Type: {@EventType}. Event handler {EventHandler} on {@EventHandlerParent}",
                "@event.StreamId",
                "@event.StreamPosition",
                "GetElapsed(start)",
                getEventType,
                method,
                "ParentType");
            ActivityHelpers.SetActivityError(_w, "ex");
            _w.WriteStatement($"if ({propagate})", "throw;");
        });
    }

    private void WriteCanHandle()
    {
        _w.WriteMethodAttributes();
        var check = _handler.EventType == null ? "true" : $"@event.Event is {_handler.EventType.CSharpName}";
        _w.WriteLine($"public bool CanHandle(global::{PureESSymbols.EventEnvelope} @event) => {check};");
    }

    private void StartActivity()
    {
        const string activityName = "HandleEvent";

        var displayName =
            $"{activityName} {ActivityHelpers.GetTypeDisplayName(_handler.Parent)}.{_handler.Method.Name}";
        if (_handler.EventType != null)
            displayName += $" ({ActivityHelpers.GetTypeDisplayName(_handler.EventType)})";
        
        ActivityHelpers.StartActivity(_w, activityName, displayName, GetTags());
    }

    private void BeginLogScope()
    {
        LoggingHelpers.BeginLogScope(_w, GetTags());
    }

    private IEnumerable<(string, string?)> GetTags() =>
    [
        ("StreamId", "@event.StreamId"),
        ("StreamPosition", "@event.StreamPosition"),
        ("HandlerClass", _handler.Parent.FullName.ToStringLiteral()),
        ("HandlerMethod", _handler.Method.Name.ToStringLiteral()),
        ("HandlerEventType", _handler.EventType?.FullName.ToStringLiteral()),
        ("EventType", EventTypeName)
    ];
    
    #region Helpers
    
    public static IEnumerable<string> GetInterfaces(EventHandler handler)
    {
        const string i = "global::PureES.IEventHandler";
        if (handler.EventType == null)
            //No event type
            return [i];
        
        //Implement all types in the inheritance hierarchy
        var types = GetBaseTypes(handler).Prepend(handler.EventType);
        return types.Select(t => $"{i}<{t.CSharpName}>");
    }
    
    private static IEnumerable<IType> GetBaseTypes(EventHandler handler)
    {
        var type = handler.EventType?.BaseType;
        //NB: We don't handle object, instead users should handle EventEnvelope if they want to handle all events
        while (type != null && type.CSharpName != "object")
        {
            yield return type;
            type = type.BaseType;
        }
    }
    
    public static string GetClassName(EventHandler handler)
    {
        var name = handler.EventType != null ? TypeNameHelpers.SanitizeName(handler.EventType) : "CatchAll";
        
        //Append the method name
        var methodName = handler.Parent.FullName.Replace(".", string.Empty);
        methodName += $"_{handler.Method.Name}";
            
        methodName = new[] { '+', '<', '>', '[', ']', '`' }.Aggregate(methodName, (s, c) => s.Replace(c, '_'));
        
        return $"{name}EventHandler_{methodName}";
    }

    private string LoggerType => ExternalTypes.ILogger(GetClassName(_handler));

    private string NullLoggerInstance => ExternalTypes.NullLoggerInstance(GetClassName(_handler));

    private static string OptionsType => ExternalTypes.IOptions(PureESSymbols.Options);

    public const string Namespace = "PureES.EventHandlers";
    private const string EventTypeName = "global::PureES.BasicEventTypeMap.GetTypeName(@event.Event.GetType())";

    #endregion
}