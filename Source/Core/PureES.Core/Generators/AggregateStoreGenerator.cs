using System.ComponentModel;
using PureES.Core.EventStore;
using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;

namespace PureES.Core.Generators;

internal class AggregateStoreGenerator
{
    public static string Generate(Aggregate aggregate) => new AggregateStoreGenerator(aggregate).GenerateInternal();
    
    private readonly IndentedWriter _w = new();

    private readonly Aggregate _aggregate;
    private readonly IReadOnlyList<IType> _services;
    
    private AggregateStoreGenerator(Aggregate aggregate)
    {
        _aggregate = aggregate;
        _services = aggregate.When
            .SelectMany(w => w.Services)
            .Distinct()
            .ToList();
    }

    private string GenerateInternal()
    {
        _w.WriteFileHeader(false);
        
        _w.WriteLine("using System.Threading.Tasks;"); //Enable WithCancellation extension method
        _w.WriteLine("using Microsoft.Extensions.Logging;"); //Enable log extension methods

        _w.WriteLine();
        
        _w.WriteLine("namespace PureES.AggregateStores");
        _w.PushBrace();

        _w.WriteClassAttributes(EditorBrowsableState.Never);
        _w.WriteLine($"internal class {ClassName} : IAggregateStore<{_aggregate.Type.CSharpName}>");
        _w.PushBrace();
        
        WriteConstructor();
        
        WriteNonFactoryMethods();
        
        WriteFactory();
        
        _w.PopAllBraces();

        return _w.Value;
    }

    private void WriteConstructor()
    {
        _w.WriteLine($"private readonly {EventStoreType} _eventStore;");

        for (var i = 0; i < _services.Count; i++)
            _w.WriteLine($"private readonly {_services[i].CSharpName} _service{i};");
        
        _w.WriteMethodAttributes();
        _w.Write($"public {ClassName}(");

        _w.WriteParameters(_services.Select((t, i) => $"{t.CSharpName} service{i}"),
            new []
            {
                $"{EventStoreType} eventStore"
            });

        _w.WriteRawLine(")");
        _w.PushBrace();

        _w.WriteLine("this._eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));");
        for (var i = 0; i < _services.Count; i++)
            _w.WriteLine($"this._service{i} = service{i} ?? throw new ArgumentNullException(nameof(service{i}));");
        
        _w.PopBrace();
    }

    private void WriteNonFactoryMethods()
    {
        var taskName = GetTaskType(_aggregate.Type.CSharpName);

        var forwards = $"global::{typeof(Direction).FullName}.{Direction.Forwards}";
        
        _w.WriteMethodAttributes();
        _w.WriteStatement($"public {taskName} Load(string streamId, CancellationToken cancellationToken)",
            () =>
            {
                _w.WriteLine($"var @events = this._eventStore.{nameof(IEventStore.Read)}({forwards}, streamId, cancellationToken);");
                _w.WriteLine("return Create(@events, cancellationToken);");
            });

        _w.WriteMethodAttributes();
        _w.WriteStatement(
            $"public {taskName} Load(string streamId, ulong expectedRevision, CancellationToken cancellationToken)",
            () =>
            {
                _w.WriteLine($"var @events = this._eventStore.{nameof(IEventStore.Read)}({forwards}, streamId, expectedRevision, cancellationToken);");
                _w.WriteLine("return Create(@events, cancellationToken);");
            });
        
        _w.WriteMethodAttributes();
        _w.WriteStatement(
            $"public {taskName} {nameof(IAggregateStore<int>.LoadPartial)}(string streamId, ulong requiredRevision, CancellationToken cancellationToken)",
            () =>
            {
                _w.WriteLine($"var @events = this._eventStore.{nameof(IEventStore.ReadPartial)}({forwards}, streamId, requiredRevision, cancellationToken);");
                _w.WriteLine("return Create(@events, cancellationToken);");
            });
    }

    private void WriteFactory()
    {
        _w.WriteMethodAttributes();

        _w.WriteStatement(
            $"public async {GetTaskType(_aggregate.Type.CSharpName)} {nameof(IAggregateStore<int>.Create)}({AsyncEnumerableEventEnvelope} @events, CancellationToken cancellationToken)",
            () =>
            {
                
                //So that we can generate the create/update, we have to implement the MoveNext methods manually

                _w.WriteStatement($"await using (var enumerator = await @events.{nameof(IAsyncEnumerable<int>.GetAsyncEnumerator)}(cancellationToken))", () =>
                {
                    const string moveNext = $"await enumerator.{nameof(IAsyncEnumerator<int>.MoveNextAsync)}()";
        
                    //Get the first item, or throw
                    _w.WriteStatement($"if (!{moveNext})",
                        "throw new ArgumentException(\"Stream is empty\", nameof(@events));");

                    _w.WriteLine($"{_aggregate.Type.CSharpName} aggregate;");
                    
                    WriteWhenSwitch(_aggregate.When.Where(w => w.IsCreate).ToList(), "CreateWhen");

                    _w.WriteStatement($"while ({moveNext})", () =>
                    {
                        WriteWhenSwitch(_aggregate.When.Where(w => !w.IsCreate).ToList(), "UpdateWhen");
                    });
                    _w.WriteLine("return aggregate;");
                });

            });
    }

    private void WriteWhenSwitch(IReadOnlyList<When> source, string fallthroughName)
    {
        _w.WriteStatement("switch (enumerator.Current.Event)", () =>
        {
            //First write generic types

            foreach (var w in source.Where(w => w.Event != null))
            {
                _w.WriteStatement($"case {w.Event!.CSharpName} e:", () =>
                {
                    InvokeWhen(w);
                    _w.WriteLine("break;");
                });
            }

            _w.WriteStatement("default:", () =>
            {
                //Fallthrough error
                _w.WriteLine("var eventType = enumerator.Current.Event.GetType().FullName;");
                _w.WriteLine(
                    $"throw new NotImplementedException($\"No suitable {fallthroughName} method found for event {{eventType}}\");");
            });
        });
        
        //Post handlers
        foreach (var w in _aggregate.When.Where(w => !w.IsCreate && w.Event == null))
            InvokeWhen(w);
    }

    private void InvokeWhen(When when)
    {
        
        if (when.IsCreate)
        {
            if (when.Method.ReturnType == null) throw new NotImplementedException();
            var async = when.Method.ReturnType.IsAsync(out _) ? "await " : string.Empty;
            _w.Write($"aggregate = {@async}{_aggregate.Type.CSharpName}.{when.Method.Name}(");
        }
        else
        {
            var async = when.Method.ReturnType != null && when.Method.ReturnType.IsAsync(out _)
                ? "await "
                : string.Empty;
            _w.Write($"{@async}aggregate.{when.Method.Name}(");
        }

        var parameters = when.Method.Parameters.Select(p =>
        {
            if (p.Type.IsNonGenericEventEnvelope())
                return "enumerator.Current";
            if (p.HasAttribute<EventAttribute>())
            {
                if (when.Event == null || !p.Type.Equals(when.Event))
                    throw new NotImplementedException();
                return "e"; //This is a local variable from switch statement
            }
            if (p.Type.IsGenericEventEnvelope(out var e, out _))
            {
                if (when.Event == null || !e.Equals(when.Event))
                    throw new NotImplementedException();
                return $"new {p.Type.CSharpName}(enumerator.Current)"; //Create envelope from non-generic envelope current
            }

            if (p.HasFromServicesAttribute())
            {
                var index = _services.IndexOf(p.Type);
                return $"this._service{index}";
            }

            if (p.Type.IsCancellationToken())
                return "cancellationToken";
            
            throw new NotImplementedException("Unknown parameter");
        });

        _w.WriteParameters(parameters);

        _w.WriteRawLine(");");
    }
    
    #region Names

    private string ClassName => $"{TypeNameHelpers.SanitizeName(_aggregate.Type.Name)}AggregateStore";

    private static string EventStoreType => $"global::{typeof(IEventStore).FullName}";

    private static string GetTaskType(string genericParameter) =>
        TypeNameHelpers.GetGenericTypeName(typeof(Task<>), genericParameter);

    private static string AsyncEnumerableEventEnvelope => 
        TypeNameHelpers.GetGenericTypeName(typeof(IAsyncEnumerable<>), $"global::{typeof(EventEnvelope)}");

    #endregion
}