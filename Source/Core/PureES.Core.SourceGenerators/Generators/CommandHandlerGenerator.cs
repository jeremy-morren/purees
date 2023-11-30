﻿using PureES.Core.SourceGenerators.Framework;
using PureES.Core.SourceGenerators.Generators.Models;

// ReSharper disable StringLiteralTypo

namespace PureES.Core.SourceGenerators.Generators;

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

        GeneratorHelpers.WriteGetElapsed(_w, false);

        _w.WriteLine();
        _w.WriteLine($"private static readonly global::{typeof(Type).FullName} AggregateType = typeof({_aggregate.Type.CSharpName});");
        _w.WriteLine($"private static readonly global::{typeof(Type).FullName} CommandType = typeof({_handler.Command.CSharpName});");
        
        _w.WriteMethodAttributes();
        
        var returnType = _handler.ResultType != null ? _handler.ResultType.CSharpName : "ulong";
        returnType = TypeNameHelpers.GetGenericTypeName(typeof(Task<>), returnType);
        _w.WriteStatement(
            $"public async {returnType} Handle({_handler.Command.CSharpName} command, CancellationToken cancellationToken)",
            WriteHandler);
        
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
            //NB: These are last, so we can provide default values of null
            new[]
            {
                $"{StreamIdSvc} getStreamId",
                $"{EventStoreType} eventStore",
                $"{AggregateStoreType} aggregateStore",
                $"{EnumerableEventEnricherType} syncEnrichers",
                $"{EnumerableAsyncEventEnricherType} asyncEnrichers",
                $"{EnumerableValidatorType} syncValidators",
                $"{EnumerableAsyncValidatorType} asyncValidators",
                $"{OptimisticConcurrencyType} concurrency = null",
                $"{LoggerType} logger = null"
            });

        _w.WriteRawLine(')');
        _w.PushBrace();
        
        var args = new[]
        {
            "getStreamId",
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
            //Add null check, which returns immediately
            _w.WriteStatement("if (command == null)", "throw new ArgumentNullException(nameof(command));");
        }

        const string commandType = "CommandType";
        const string aggregateType = "AggregateType";
        
        _w.WriteLogMessage("Debug",
            "null",
            "Handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}", 
            commandType, aggregateType, method);

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

            var result = _handler.ResultType != null ? $"result?.{nameof(CommandResult<int, int>.Event)}" : "result";
            
            _w.WriteStatement($"if ({result} != null)", () =>
            {
                if (_handler.EventType.IsEventsTransaction())
                {
                    WriteCreateTransaction();
                    
                    _w.WriteStatement("if (transaction.Count > 0)", () =>
                    {
                        WriteEnrich(true, $"transaction.Values.SelectMany(p => p.{nameof(UncommittedEventsList.Events)})");
                        _w.WriteLine($"await _eventStore.{nameof(IEventStore.SubmitTransaction)}(transaction, cancellationToken);");
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
                        ? $"revision = await _eventStore.{nameof(IEventStore.Append)}(streamId, currentRevision, {source}, cancellationToken);" 
                        : $"revision = await _eventStore.{nameof(IEventStore.Create)}(streamId, {source}, cancellationToken);");
                
                    if (isEnumerable)
                        _w.PopBrace(); //if statement above
                }
            });

            _w.WriteLine($"this._concurrency?.{nameof(IOptimisticConcurrency.OnUpdated)}(streamId, command, {(_handler.IsUpdate ? "currentRevision" : "null")}, revision);");
            
            _w.WriteLogMessage("Information",
                "null",
                "Handled command {@Command}. Elapsed: {Elapsed:0.0000}ms. Stream {StreamId} is now at {Revision}. Aggregate: {@Aggregate}. Method: {Method}",
                commandType, "GetElapsed(start)", "streamId", "revision", aggregateType, method);
            
            _w.WriteLine(_handler.ResultType != null ? $"return result?.{nameof(CommandResult<int,int>.Result)};" : "return revision;");
        });
        _w.WriteStatement($"catch (global::{typeof(Exception).FullName} ex)", () =>
        {
            //Note: We write 'Information' here, so avoid duplicate error messages when it is caught by global request exception handler
            _w.WriteLogMessage("Information",
                "ex",
                "Error handling command {@Command}. Aggregate: {@Aggregate}. Method: {Method}. Elapsed: {Elapsed:0.0000}ms",
                commandType, aggregateType, method, "GetElapsed(start)");
            _w.WriteLine("throw;");
        });
    }

    private void WriteValidate()
    {
        _w.WriteStatement("foreach (var v in this._syncValidators)",
            $"v.{nameof(ICommandValidator<object>.Validate)}(command);");
        _w.WriteStatement("foreach (var v in this._asyncValidators)",
            $"await v.{nameof(IAsyncCommandValidator<object>.Validate)}(command, cancellationToken);");
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
        var newEvent = $"new {type}(e)";
        
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
        _w.WriteLine($"var e = new {type}({source});");
        return false;
    }

    private void WriteEnrich(bool isEnumerable, string source)
    {
        _w.WriteStatement("foreach (var enricher in this._syncEnrichers)", () =>
        {
            const string enrich = nameof(IEventEnricher.Enrich);
            if (isEnumerable)
                _w.WriteStatement($"foreach (var e in {source})", $"enricher.{enrich}(e);");
            else
                _w.WriteLine($"enricher.{enrich}({source});");
        });

        _w.WriteStatement("foreach (var enricher in this._asyncEnrichers)", () =>
        {
            const string enrich = nameof(IAsyncEventEnricher.Enrich);
            if (isEnumerable)
                _w.WriteStatement($"foreach (var e in {source})",
                    $"await enricher.{enrich}(e, cancellationToken);");
            else
                _w.WriteLine($"await enricher.{enrich}({source}, cancellationToken);");
        });
    }

    private void WriteCreateTransaction()
    {
        var list = $"global::{typeof(UncommittedEventsList).FullName}";
        
        var source = _handler.ResultType != null ? $"result.{nameof(CommandResult<int,int>.Event)}" : "result";
        
        var dictionary = TypeNameHelpers.GetGenericTypeName(typeof(Dictionary<,>), "string", list);
        
        _w.WriteLine($"var transaction = new {dictionary}();");
        
        _w.WriteStatement($"foreach (var pair in {source})", () =>
        {
            //If stream is current stream, use currentRevision as expected
            //And manually calculate return revision
            _w.WriteStatement("if (pair.Key == streamId)", () =>
            {
                _w.WriteLine($"transaction.Add(pair.Key, new {list}(currentRevision, pair.Value));");
                _w.WriteLine(_handler.IsUpdate
                    ? "revision = currentRevision + (ulong)(pair.Value.Count - 1);"
                    : "revision = (ulong)pair.Value.Count - 1;");
            });
            _w.WriteStatement("else", 
                $"transaction.Add(pair.Key, new {list}(pair.Value.{nameof(UncommittedEventsList.ExpectedRevision)}, pair.Value));");
        });
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
            _w.WriteLine($"var streamId = this._getStreamId.{nameof(ICommandStreamId<int>.GetStreamId)}(command);");
        }
    }

    #region Types

    public const string Namespace = "PureES.CommandHandlers";
    
    public static string GetClassName(CommandHandler handler) => $"{TypeNameHelpers.SanitizeName(handler.Command)}CommandHandler";

    public static string GetInterface(CommandHandler handler)
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
    private string NullLoggerInstance => ExternalTypes.NullLoggerInstance(ClassName);
    
    private string StreamIdSvc =>
        TypeNameHelpers.GetGenericTypeName(typeof(ICommandStreamId<>), _handler.Command.CSharpName);
    
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