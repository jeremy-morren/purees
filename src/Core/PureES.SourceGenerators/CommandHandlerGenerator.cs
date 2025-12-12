using PureES.SourceGenerators.Framework;
using PureES.SourceGenerators.Models;

// ReSharper disable StringLiteralTypo

namespace PureES.SourceGenerators;

internal class CommandHandlerGenerator
{
    public static string Generate(Aggregate aggregate, CommandHandler handler, out string filename)
    {
        filename = $"{Namespace}.{GetClassName(handler)}";
        var generator = new CommandHandlerGenerator(aggregate, handler);
        return generator.Generate();
    }

    private readonly IndentedWriter _w = new();
    private readonly Aggregate _aggregate;
    private readonly CommandHandler _handler;

    private CommandHandlerGenerator(Aggregate aggregate, CommandHandler handler)
    {
        _aggregate = aggregate;
        _handler = handler;
    }

    private string Generate()
    {
        _w.WriteFileHeader(false);

        _w.WriteLine();
        
        _w.WriteLine($"namespace {Namespace}");
        _w.PushBrace();
        
        _w.WriteClassAttributes();
        _w.WriteLine($"internal sealed class {ClassName} : {Interface}");
        _w.PushBrace();

        WriteConstructor();

        WriteGetStreamId();

        GeneratorHelpers.WriteGetElapsed(_w, false);

        _w.WriteLine();
        _w.WriteLine($"private static readonly global::{typeof(Type).FullName} AggregateType = typeof({_aggregate.Type.CSharpName});");
        _w.WriteLine($"private static readonly global::{typeof(Type).FullName} CommandType = typeof({_handler.Command.CSharpName});");

        _w.WriteMethodAttributes();
        
        var returnType = _handler.ResultType != null ? _handler.ResultType.CSharpName : "uint";
        returnType = TypeNameHelpers.GetGenericTypeName(typeof(Task<>), returnType);
        _w.WriteStatement(
            $"public async {returnType} Handle({_handler.Command.CSharpName} command, CancellationToken cancellationToken)",
            () =>
            {
                _w.CheckNotNull("command");
                WriteHandler();
            });
        
        _w.PopAllBraces();
        
        return _w.Value;
    }

    private void WriteConstructor()
    {
        //Write service definitions

        //Only need StreamId svc if stream id is not provided
        if (_handler.StreamId == null)
            _w.WriteLine($"private readonly {StreamIdSvc} _getStreamId;");
        
        _w.WriteLine($"private readonly {AggregateStoreType} _aggregateStore;");
        _w.WriteLine($"private readonly {EventStoreType} _eventStore;");
        _w.WriteLine($"private readonly {ConcurrencyType} _concurrency;");
        _w.WriteLine($"private readonly {EnumerableEventEnricherType} _syncEnrichers;");
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
            
            _handler.StreamId == null ? [$"{StreamIdSvc} getStreamId"] : [],

            [
                $"{EventStoreType} eventStore",
                $"{AggregateStoreType} aggregateStore",
                $"{EnumerableEventEnricherType} syncEnrichers",
                $"{EnumerableAsyncEventEnricherType} asyncEnrichers",
                $"{EnumerableValidatorType} syncValidators",
                $"{EnumerableAsyncValidatorType} asyncValidators",

                //NB: These are last, so we can provide default values of null
                $"{ConcurrencyType} concurrency = null",
                $"{LoggerType} logger = null"
            ]);

        _w.WriteRawLine(')');
        _w.PushBrace();
        
        if (_handler.StreamId == null)
            _w.WriteLine("this._getStreamId = getStreamId ?? throw new ArgumentNullException(nameof(getStreamId));");
        
        var args = new[]
        {
            "eventStore",
            "aggregateStore",
            "syncEnrichers",
            "asyncEnrichers",
            "syncValidators",
            "asyncValidators"
        };
        foreach (var a in args)
            _w.WriteLine($"this._{a} = {a} ?? throw new ArgumentNullException(nameof({a}));");
        
        _w.WriteLine("this._concurrency = concurrency;");
        _w.WriteLine($"this._logger = logger ?? {NullLoggerInstance};");
        
        for (var i = 0; i < _handler.Services.Length; i++)
            _w.WriteLine($"this._service{i} = service{i} ?? throw new ArgumentNullException(nameof(service{i}));");
        
        _w.PopBrace();
    }

    private void WriteHandler()
    {
        var method = $"\"{_handler.Method.Name}\"";

        if (_handler.Command.IsReferenceType)
        {
            //Add null check
            _w.WriteStatement("if (command == null)", "throw new ArgumentNullException(nameof(command));");
        }

        const string commandType = "CommandType";
        const string aggregateType = "AggregateType";
        
        _w.WriteLogMessage("Debug",
            "null",
            "Handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}", 
            commandType, aggregateType, method);

        WriteStartActivity();

        BeginLogScope();

        _w.WriteLine($"var start = {GeneratorHelpers.GetTimestamp};");

        _w.WriteStatement("try", () =>
        {
            WriteValidate();
            
            WriteSetStreamId();

            if (_handler.IsUpdate)
            {
                //Get revision & Load
                _w.WriteLine(
                    "var currentRevision = this._concurrency?.GetExpectedRevision(streamId, command) ?? await this._eventStore.GetRevision(streamId, cancellationToken);");
                _w.WriteLine("var current = await _aggregateStore.Load(streamId, currentRevision, cancellationToken);");
            }
            
            WriteInvoke();

            _w.WriteLine(_handler.IsUpdate ? "var revision = currentRevision;" : "var revision = uint.MaxValue;");

            var result = _handler.ResultType != null ? "result?.Event" : "result";
            
            _w.WriteStatement($"if ({result} != null)", () =>
            {
                if (_handler.EventType.IsEventsTransaction())
                {
                    WriteCreateTransaction();
                    
                    _w.WriteStatement("if (transaction.Count > 0)", () =>
                    {
                        WriteEnrich(true, "transaction.SelectMany(l => l)");
                        _w.WriteLine("await _eventStore.SubmitTransaction(transaction, cancellationToken);");
                    });
                }
                else
                {
                    var isEnumerable = WriteCreateEventsList();
                    
                    if (isEnumerable)
                    {
                        _w.WriteLine("if (events.Count > 0)");
                        _w.PushBrace();
                    }
                    var source = isEnumerable ? "events" : "e";
                    
                    WriteEnrich(isEnumerable, source);
                
                    _w.WriteLine(_handler.IsUpdate 
                        ? $"revision = await _eventStore.Append(streamId, currentRevision, {source}, cancellationToken);" 
                        : $"revision = await _eventStore.Create(streamId, {source}, cancellationToken);");
                
                    if (isEnumerable)
                        _w.PopBrace(); //if statement above
                }
            });

            _w.WriteLine($"this._concurrency?.OnUpdated(streamId, command, {(_handler.IsUpdate ? "currentRevision" : "null")}, revision);");
            
            _w.WriteLogMessage("Information",
                "null",
                "Handled command {@Command}. Elapsed: {Elapsed:0.0000}ms. Stream {StreamId} is now at {Revision}. Aggregate: {@Aggregate}. Method: {Method}",
                commandType, "GetElapsed(start)", "streamId", "revision", aggregateType, method);
            
            _w.WriteLine(_handler.ResultType != null ? "return result?.Result;" : "return revision;");

            ActivityHelpers.SetActivitySuccess(_w);
        });
        _w.WriteStatement($"catch (global::{typeof(Exception).FullName} ex)", () =>
        {
            //Note: We write 'Information' here, so avoid duplicate error messages when it is caught by global request exception handler
            _w.WriteLogMessage("Information",
                "ex",
                "Error handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}. Elapsed: {Elapsed:0.0000}ms",
                commandType, aggregateType, method, "GetElapsed(start)");
            ActivityHelpers.SetActivityError(_w, "ex");
            _w.WriteLine("throw;");
        });

        _w.PopBrace(); //Log scope

        _w.PopBrace(); //Activity
    }

    private void WriteValidate()
    {
        _w.WriteStatement("foreach (var v in this._syncValidators)", "v.Validate(command);");
        _w.WriteStatement("foreach (var v in this._asyncValidators)",
            "await v.Validate(command, cancellationToken);");
    }

    private void WriteGetStreamId()
    {
        _w.WriteMethodAttributes();
        _w.WriteStatement($"public string GetStreamId({_handler.Command.CSharpName} command)", () =>
        {
            _w.WriteStatement("if (command == null)", "throw new ArgumentNullException(nameof(command));");

            // If stream id was provided, then a constant, otherwise invoke service
            _w.WriteLine(_handler.StreamId != null
                ? $"return {_handler.StreamId.ToStringLiteral()};"
                : "return this._getStreamId.GetStreamId(command);");
        });
    }

    private void WriteInvoke()
    {
        var await = _handler.IsAsync ? "await " : null;
        
        var parent = _handler.Method.IsStatic ? _aggregate.Type.CSharpName : "current";
        
        _w.Write($"var result = {await}{parent}.{_handler.Method.Name}(");
        var parameters = _handler.Method.Parameters
            .Select(p =>
            {
                if (p.HasCommandAttribute())
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
        const string type = $"global::{PureESSymbols.UncommittedEvent}";
        const string list = $"var events = new List<{type}>();";
        const string newEvent = $"new {type}(e)";
        
        //local var 'result' is the source
        var source = _handler.ResultType != null ? "result.Event" : "result";
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
        _w.WriteLine($"var e = new {type}({source});");
        return false;
    }

    private void WriteEnrich(bool isEnumerable, string source)
    {
        _w.WriteStatement("foreach (var enricher in this._syncEnrichers)", () =>
        {
            if (isEnumerable)
                _w.WriteStatement($"foreach (var e in {source})", "enricher.Enrich(e);");
            else
                _w.WriteLine($"enricher.Enrich({source});");
        });

        _w.WriteStatement("foreach (var enricher in this._asyncEnrichers)", () =>
        {
            if (isEnumerable)
                _w.WriteStatement($"foreach (var e in {source})",
                    $"await enricher.Enrich(e, cancellationToken);");
            else
                _w.WriteLine($"await enricher.Enrich({source}, cancellationToken);");
        });
    }

    private void WriteCreateTransaction()
    {
        const string list = $"global::{PureESSymbols.UncommittedEventsList}";

        _w.WriteLine($"var transaction = new {TypeNameHelpers.GetGenericTypeName(typeof(List<>), list)}();");

        var source = _handler.ResultType != null ? "result.Event" : "result";
        _w.WriteStatement($"foreach (var pair in {source})", () =>
        {
            //If stream is current stream, manually calculate return revision
            _w.WriteStatement("if (pair.Key == streamId)", 
                "revision = pair.Value.ExpectedRevision.HasValue ? pair.Value.ExpectedRevision.Value + (uint)pair.Value.Count : (uint)(pair.Value.Count - 1);");
            
            _w.WriteStatement("if (pair.Value.Count > 0)",
                $"transaction.Add(new {list}(pair.Key, pair.Value.ExpectedRevision, pair.Value));");
        });
    }
    
    private void WriteSetStreamId()
    {
        //If stream id was provided, then a constant
        //Otherwise invoke service
        _w.WriteLine(_handler.StreamId != null
            ? $"const string streamId = {_handler.StreamId.ToStringLiteral()};"
            : "var streamId = this._getStreamId.GetStreamId(command);");
    }

    private void WriteStartActivity()
    {
        const string activityName = "HandleCommand";
        var displayName = $"{activityName} {ActivityHelpers.GetTypeDisplayName(_aggregate.Type)}.{_handler.Method.Name}";
        ActivityHelpers.StartActivity(_w, activityName, displayName, GetTags());
    }
    
    private void BeginLogScope()
    {
        LoggingHelpers.BeginLogScope(_w, GetTags());
    }

    private IEnumerable<(string, string?)> GetTags() =>
    [
        ("Command", "CommandType"),
        ("Aggregate","AggregateType"),
        ("Method", _handler.Method.Name.ToStringLiteral())
    ];

    #region Types

    public const string Namespace = "PureES.CommandHandlers";
    
    public static string GetClassName(CommandHandler handler) => $"{TypeNameHelpers.SanitizeName(handler.Command)}CommandHandler";

    public static string GetInterface(CommandHandler handler)
    {
        return handler.ResultType != null
            ? $"global::{PureESSymbols.ICommandHandler}<{handler.Command.CSharpName}, {handler.ResultType.CSharpName}>"
            : $"global::{PureESSymbols.ICommandHandler}<{handler.Command.CSharpName}>";
    }

    private string ClassName => GetClassName(_handler);
    private string Interface => GetInterface(_handler);

    private static string EventStoreType => $"global::{PureESSymbols.IEventStore}";
    
    private static string ConcurrencyType => $"global::{PureESSymbols.IConcurrency}";

    private string LoggerType => ExternalTypes.ILogger(ClassName);
    private string NullLoggerInstance => ExternalTypes.NullLoggerInstance(ClassName);
    
    private string StreamIdSvc => $"{PureESSymbols.ICommandStreamId}<{_handler.Command.CSharpName}>";
    
    private string AggregateStoreType => $"{PureESSymbols.IAggregateStore}<{_aggregate.Type.CSharpName}>";

    private static string EnumerableEventEnricherType => 
        TypeNameHelpers.GetGenericTypeName(typeof(IEnumerable<>), $"global::{PureESSymbols.IEventEnricher}");
    
    private static string EnumerableAsyncEventEnricherType => 
        TypeNameHelpers.GetGenericTypeName(typeof(IEnumerable<>), $"global::{PureESSymbols.IAsyncEventEnricher}");

    private string EnumerableValidatorType =>
        TypeNameHelpers.GetGenericTypeName(typeof(IEnumerable<>),
            $"global::{PureESSymbols.ICommandValidator}<{_handler.Command.CSharpName}>");

    private string EnumerableAsyncValidatorType =>
        TypeNameHelpers.GetGenericTypeName(typeof(IEnumerable<>),
            $"global::{PureESSymbols.IAsyncCommandValidator}<{_handler.Command.CSharpName}>");
    
    #endregion
}