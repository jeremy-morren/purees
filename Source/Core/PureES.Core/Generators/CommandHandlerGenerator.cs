using System.ComponentModel;
using PureES.Core.EventStore;
using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;

// ReSharper disable StringLiteralTypo

namespace PureES.Core.Generators;

internal class CommandHandlerGenerator
{
    public static string Generate(Aggregate aggregate, Handler handler, out string filename)
    {
        filename = $"{Namespace}.{GetClassName(handler)}";
        var generator = new CommandHandlerGenerator(aggregate, handler);
        return generator.Generate();
    }

    private readonly IndentedWriter _w = new();
    private readonly Aggregate _aggregate;
    private readonly Handler _handler;

    private CommandHandlerGenerator(Aggregate aggregate, Handler handler)
    {
        _aggregate = aggregate;
        _handler = handler;
    }

    private string Generate()
    {
        _w.WriteFileHeader(false);

        _w.WriteLine("using System.Threading.Tasks;"); //Enable WithCancellation extension method
        _w.WriteLine($"using {ExternalTypes.LoggingNamespace};"); //Enable log extension methods

        _w.WriteLine();
        
        _w.WriteLine($"namespace {Namespace}");
        _w.PushBrace();
        
        _w.WriteClassAttributes(EditorBrowsableState.Never);
        _w.WriteLine($"internal class {ClassName} : {Interface}");
        _w.PushBrace();

        WriteConstructor();

        _w.WriteLine();

        GeneratorHelpers.WriteGetElapsed(_w, false);

        _w.WriteLine();

        _w.WriteMethodAttributes();

        var returnType = _handler.ResultType != null ? _handler.ResultType.CSharpName : "ulong";
        returnType = TypeNameHelpers.GetGenericTypeName(typeof(Task<>), returnType);
        _w.WriteLine(
            $"public async {returnType} Handle({_handler.Command.CSharpName} command, CancellationToken cancellationToken)");
        
        _w.PushBrace();

        WriteHandler();
        
        _w.PopAllBraces();

        return _w.Value;
    }

    private void WriteConstructor()
    {
        //Write service definitions

        _w.WriteLine($"private readonly {StreamIdSvc} _getStreamId;");
        _w.WriteLine($"private readonly {AggregateStoreType} _aggregateStore;");
        _w.WriteLine($"private readonly {EventStoreType} _eventStore;");
        _w.WriteLine($"private readonly {OptimisticConcurrencyType} _concurrency;");
        _w.WriteLine($"private readonly {EnumerableEventEnricherType} _enrichers;");
        _w.WriteLine($"private readonly {EnumerableAsyncEventEnricherType} _asyncEnrichers;");
        _w.WriteLine($"private readonly {EnumerableValidatorType} _syncValidators;");
        _w.WriteLine($"private readonly {EnumerableAsyncValidatorType} _asyncValidators;");
        _w.WriteLine($"private readonly {LoggerType} _logger;");

        for (var i = 0; i < _handler.Services.Length; i++)
            _w.WriteLine($"private readonly {_handler.Services[i].CSharpName} _service{i};");
        
        //Generate constructor for DI
        _w.WriteMethodAttributes();
        _w.Write($"public {ClassName}(");
        
        _w.WriteParameters(
            _handler.Services.Select((svc, i) => $"{svc.CSharpName} service{i}"),
            //NB: These are last, so we can provide default values of null
            new[]
            {
                $"{StreamIdSvc} getStreamId",
                $"{EventStoreType} eventStore",
                $"{AggregateStoreType} aggregateStore",
                $"{OptimisticConcurrencyType} concurrency = null",
                $"{EnumerableEventEnricherType} enrichers = null",
                $"{EnumerableAsyncEventEnricherType} asyncEnrichers = null",
                $"{EnumerableValidatorType} syncValidators = null",
                $"{EnumerableAsyncValidatorType} asyncValidators = null",
                $"{LoggerType} logger = null"
            });

        _w.WriteRawLine(')');
        _w.PushBrace();
        _w.WriteLine("this._getStreamId = getStreamId ?? throw new ArgumentNullException(nameof(getStreamId));");
        _w.WriteLine("this._eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));");
        _w.WriteLine("this._aggregateStore = aggregateStore ?? throw new ArgumentNullException(nameof(aggregateStore));");

        _w.WriteLine("this._concurrency = concurrency;");
        _w.WriteLine("this._enrichers = enrichers;");
        _w.WriteLine("this._asyncEnrichers = asyncEnrichers;");
        _w.WriteLine("this._syncValidators = syncValidators;");
        _w.WriteLine("this._asyncValidators = asyncValidators;");
        _w.WriteLine("this._logger = logger;");
        
        for (var i = 0; i < _handler.Services.Length; i++)
            _w.WriteLine($"this._service{i} = service{i} ?? throw new ArgumentNullException(nameof(service{i}));");
        
        _w.PopBrace();
        
        _w.WriteLine();
    }

    private void WriteHandler()
    {
        var method = $"\"{_handler.Method.Name}\"";

        if (_handler.Command.IsReferenceType)
        {
            //Add null check, which returns immediately
            _w.WriteStatement("if (command == null)", "throw new ArgumentNullException(nameof(command));");
        }

        _w.WriteLine($"var commandType = typeof({_handler.Command.CSharpName});");
        _w.WriteLine($"var aggregateType = typeof({_aggregate.Type.CSharpName});");
        
        _w.WriteLogMessage("Debug",
            "null",
            "Handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}", 
            "commandType", "aggregateType", method);

        _w.WriteLine($"var start = {GeneratorHelpers.GetTimestamp};");
        
        _w.WriteStatement("try", () =>
        {
            WriteValidate();
            
            WriteGetStreamId();

            if (_handler.IsUpdate)
            {
                //Get revision & Load
                _w.WriteLine(
                    $"var currentRevision = this._concurrency?.{nameof(IOptimisticConcurrency.GetExpectedRevision)}(streamId, command) ?? await this._eventStore.{nameof(IEventStore.GetRevision)}(streamId, cancellationToken);");
                _w.WriteLine($"var current = await _aggregateStore.{nameof(IAggregateStore<int>.Load)}(streamId, currentRevision, cancellationToken);");
            }
            
            WriteInvoke();

            _w.WriteLine(_handler.IsUpdate ? "var revision = currentRevision;" : "var revision = ulong.MaxValue;");
            
            _w.WriteStatement(_handler.ResultType != null ? $"if (result?.{nameof(CommandResult<int,int>.Event)} != null)" : "if (result != null)", () =>
            {
                var isEnumerable = WriteCreateEventsList();

                if (isEnumerable)
                {
                    _w.WriteLine("if (events.Count > 0)");
                    _w.PushBrace();
                }
                WriteEnrich(isEnumerable);
                
                var eventParam = isEnumerable ? "events" : "e";
                _w.WriteLine(_handler.IsUpdate 
                    ? $"revision = await _eventStore.{nameof(IEventStore.Append)}(streamId, currentRevision, {eventParam}, cancellationToken);" 
                    : $"revision = await _eventStore.{nameof(IEventStore.Create)}(streamId, {eventParam}, cancellationToken);");
                
                if (isEnumerable)
                    _w.PopBrace(); //if statement above
            });

            _w.WriteLine($"this._concurrency?.{nameof(IOptimisticConcurrency.OnUpdated)}(streamId, command, {(_handler.IsUpdate ? "currentRevision" : "null")}, revision);");
            
            _w.WriteLogMessage("Information",
                "null",
                "Handled command {@Command}. Elapsed: {Elapsed:0.0000}ms. Stream {StreamId} is now at {Revision}. Aggregate: {@Aggregate}. Method: {Method}",
                "commandType", "GetElapsed(start)", "streamId", "revision", "aggregateType", method);
            
            
            _w.WriteLine(_handler.ResultType != null ? $"return result?.{nameof(CommandResult<int,int>.Result)};" : "return revision;");
        });
        _w.WriteStatement($"catch (global::{typeof(Exception).FullName} ex)", () =>
        {
            //Note: We write 'Information' here, so avoid duplicate error messages when it is caught by global request exception handler
            _w.WriteLogMessage("Information",
                "ex",
                "Error handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}. Elapsed: {Elapsed:0.0000}ms",
                "commandType", "aggregateType", method, "GetElapsed(start)");
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

    private void WriteInvoke()
    {
        var await = _handler.IsAsync ? "await " : null;
        
        var parent = _handler.Method.IsStatic ? _aggregate.Type.CSharpName : "current";
        
        _w.Write($"var result = {@await}{parent}.{_handler.Method.Name}(");
        var parameters = _handler.Method.Parameters
            .Select(p =>
            {
                if (p.HasAttribute<CommandAttribute>())
                    return "command";
                if (p.HasFromServicesAttribute())
                    return $"this._service{_handler.Services.GetIndex(p.Type)}";
                if (p.Type.Equals(_aggregate.Type))
                    return "current";
                if (p.Type.IsCancellationToken())
                    return "cancellationToken";
                throw new NotImplementedException("Unknown parameter");
            });

        _w.WriteParameters(parameters);

        _w.WriteRawLine(");");
    }

    private bool WriteCreateEventsList()
    {
        var type = $"global::{typeof(UncommittedEvent).FullName}";
        var list = $"var events = new List<{type}>();";
        var newEvent = $"new {type}() {{ Event = e }}";
        
        //local var 'result' is the source
        var source = _handler.ResultType != null ? $"result.{nameof(CommandResult<int,int>.Event)}" : "result";
        if (_handler.EventType.IsEnumerable())
        {
            _w.WriteLine(list);
            _w.WriteStatement($"foreach (var e in {source})", 
                $"events.Add({newEvent});");
            return true;
        }
        if (_handler.EventType.IsAsyncEnumerable())
        {
            _w.WriteLine(list);
            _w.WriteStatement($"await foreach (var e in {source}.WithCancellation(cancellationToken))",
                $"events.Add({newEvent});");
            return true;
        }
        _w.WriteLine($"var e = new {type}() {{ Event = {source} }};");
        return false;
    }

    private void WriteEnrich(bool isEnumerable)
    {
        //If enumerable, local variable 'events'
        //Otherwise, local variable 'e'
        
        _w.WriteStatement("if (this._enrichers != null)",
            () => _w.WriteStatement("foreach (var enricher in this._enrichers)", () =>
            {
                const string enrich = "enricher.Enrich(e);";
                if (isEnumerable)
                    _w.WriteStatement("foreach (var e in events)", enrich);
                else
                    _w.WriteLine(enrich);
            }));
        
        _w.WriteStatement("if (this._asyncEnrichers != null)",
            () => _w.WriteStatement("foreach (var enricher in this._asyncEnrichers)", () =>
            {
                const string enrich = "await enricher.Enrich(e, cancellationToken);";
                if (isEnumerable)
                    _w.WriteStatement("foreach (var e in events)", enrich);
                else
                    _w.WriteLine(enrich);
            }));
    }

    private void WriteGetStreamId()
    {
        //If stream id was provided, then a constant
        //Otherwise invoke service
        if (_handler.StreamId != null)
        {
            var streamId = _handler.StreamId.Replace("\"", "\\\"");
            _w.WriteLine($"const string streamId = \"{streamId}\";");
        }
        else
        {
            _w.WriteLine($"var streamId = this._getStreamId.{nameof(PureESStreamId<int>.GetId)}(command);");
        }
    }

    #region Types

    public const string Namespace = "PureES.CommandHandlers";
    
    public static string GetClassName(Handler handler)
    {
        var name = handler.Command.IsGenericType
            ? handler.Command.FullName.Substring(handler.Command.Namespace?.Length ?? 0) //Remove namespace
            : handler.Command.Name;
        
        return $"{TypeNameHelpers.SanitizeName(name)}CommandHandler";
    }

    public static string GetInterface(Handler handler)
    {
        return handler.ResultType != null
            ? TypeNameHelpers.GetGenericTypeName(typeof(ICommandHandler<,>),
                handler.Command.CSharpName, handler.ResultType.CSharpName)
            : TypeNameHelpers.GetGenericTypeName(typeof(ICommandHandler<>), handler.Command.CSharpName);
    }

    private string ClassName => GetClassName(_handler);
    private string Interface => GetInterface(_handler);

    private static string EventStoreType => $"global::{typeof(IEventStore).FullName}";
    
    private static string OptimisticConcurrencyType => $"global::{typeof(IOptimisticConcurrency).FullName}";

    private string LoggerType => ExternalTypes.ILogger(ClassName);
    
    private string StreamIdSvc =>
        TypeNameHelpers.GetGenericTypeName(typeof(PureESStreamId<>), _handler.Command.CSharpName);
    
    private string AggregateStoreType =>
        TypeNameHelpers.GetGenericTypeName(typeof(IAggregateStore<>), _aggregate.Type.CSharpName);

    private static string EnumerableEventEnricherType => 
        TypeNameHelpers.GetGenericTypeName(typeof(IEnumerable<>), $"global::{typeof(IEventEnricher).FullName}");
    
    private static string EnumerableAsyncEventEnricherType => 
        TypeNameHelpers.GetGenericTypeName(typeof(IEnumerable<>), $"global::{typeof(IAsyncEventEnricher).FullName}");

    private string EnumerableValidatorType =>
        TypeNameHelpers.GetGenericTypeName(typeof(IEnumerable<>),
            TypeNameHelpers.GetGenericTypeName(typeof(ICommandValidator<>), _handler.Command.CSharpName));

    private string EnumerableAsyncValidatorType =>
        TypeNameHelpers.GetGenericTypeName(typeof(IEnumerable<>),
            TypeNameHelpers.GetGenericTypeName(typeof(IAsyncCommandValidator<>), _handler.Command.CSharpName));
    
    #endregion
}