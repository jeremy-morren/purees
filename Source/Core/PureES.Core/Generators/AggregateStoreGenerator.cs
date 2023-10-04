using System.ComponentModel;
using PureES.Core.EventStore;
using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;

namespace PureES.Core.Generators;

internal class AggregateStoreGenerator
{
    public static string Generate(Aggregate aggregate, out string filename)
    {
        filename = $"{Namespace}.{GetClassName(aggregate)}";
        return new AggregateStoreGenerator(aggregate).GenerateInternal();
    }

    private readonly IndentedWriter _w = new();

    private readonly Aggregate _aggregate;
    
    private AggregateStoreGenerator(Aggregate aggregate)
    {
        _aggregate = aggregate;
    }

    private string GenerateInternal()
    {
        _w.WriteFileHeader(false);
        
        _w.WriteLine("using System.Threading.Tasks;"); //Enable WithCancellation extension method
        _w.WriteLine("using Microsoft.Extensions.Logging;"); //Enable log extension methods
        _w.WriteLine("using Microsoft.Extensions.DependencyInjection;"); //DI extension methods

        _w.WriteLine();
        
        _w.WriteLine($"namespace {Namespace}");
        _w.PushBrace();

        _w.WriteClassAttributes(EditorBrowsableState.Never);
        _w.WriteLine($"internal class {ClassName} : {Interface}");
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
        _w.WriteLine($"private readonly {ServiceProviderType} _services;");
        
        _w.WriteMethodAttributes();
        _w.Write($"public {ClassName}(");

        _w.WriteParameters($"{EventStoreType} eventStore", $"{ServiceProviderType} services");

        _w.WriteRawLine(")");
        _w.PushBrace();

        _w.WriteLine("this._eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));");
        _w.WriteLine("this._services = services ?? throw new ArgumentNullException(nameof(services));");
        
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
                _w.WriteLine($"var @events = this._eventStore.ReadPartial({forwards}, streamId, requiredRevision, cancellationToken);");
                _w.WriteLine("return Create(@events, cancellationToken);");
            });
    }

    private void WriteFactory()
    {
        _w.WriteMethodAttributes();

        _w.WriteStatement(
            $"public async {GetTaskType(_aggregate.Type.CSharpName)} Create({AsyncEnumerableEventEnvelope} @events, CancellationToken cancellationToken)",
            () =>
            {
                
                //So that we can generate the create/update, we have to implement the MoveNext methods manually

                _w.WriteStatement("await using (var enumerator = @events.GetAsyncEnumerator(cancellationToken))", () =>
                {
                    const string moveNext = "await enumerator.MoveNextAsync()";
        
                    //Get the first item, or throw
                    _w.WriteStatement($"if (!{moveNext})",
                        "throw new ArgumentException(\"Stream is empty\", nameof(@events));");

                    _w.WriteLine($"{_aggregate.Type.CSharpName} current;");
                    
                    WriteWhenSwitch(_aggregate.When.Where(w => !w.IsUpdate).ToList(), "CreateWhen");

                    _w.WriteStatement($"while ({moveNext})", () =>
                    {
                        WriteWhenSwitch(_aggregate.When.Where(w => w.IsUpdate).ToList(), "UpdateWhen");
                    });
                    _w.WriteLine("return current;");
                });

            });
    }

    private void WriteWhenSwitch(IReadOnlyList<When> source, string fallthroughName)
    {
        _w.WriteStatement("switch (enumerator.Current.Event)", () =>
        {
            //Write strongly-typed handlers
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
                var getName = $"global::{typeof(BasicEventTypeMap).FullName}.{nameof(BasicEventTypeMap.GetTypeName)}";
                //Fallthrough error
                _w.WriteLine($"var eventType = {getName}(enumerator.Current.Event.GetType());");
                _w.WriteLine($"var aggregateType = {getName}(typeof({_aggregate.Type.CSharpName}));");
                _w.WriteLine(
                    $"throw new NotImplementedException($\"No suitable {fallthroughName} method found for event '{{eventType}}' on '{{aggregateType}}'\");");
            });
        });
        
        //catch-all handlers
        foreach (var w in _aggregate.When.Where(w => w.Event == null && !w.Method.IsStatic))
            InvokeWhen(w);
    }

    private void InvokeWhen(When when)
    {
        var async = when.Method.ReturnType != null && when.Method.ReturnType.IsAsync(out _)
            ? "await "
            : string.Empty;

        _w.Write(when.Method.IsStatic
            ? $"current = {async}{_aggregate.Type.CSharpName}.{when.Method.Name}("
            : $"{async}current.{when.Method.Name}(");
        
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
                /*
                 * We don't inject services because it causes a circular dependency
                 * (other aggregate stores used in when methods depend on this one etc)
                 */
                return p.Type.CSharpName.Equals(ServiceProviderType, StringComparison.Ordinal) 
                    ? "this._services" //Pass IServiceProvider through
                    : $"({p.Type.CSharpName})this._services.GetRequiredService(typeof({p.Type.CSharpName}))";
            }
            
            if (p.Type.Equals(_aggregate.Type))
                return "current"; //Provide current aggregate parameter

            if (p.Type.IsCancellationToken())
                return "cancellationToken";
            
            throw new NotImplementedException("Unknown parameter");
        });

        _w.WriteParameters(parameters);

        _w.WriteRawLine(");");
    }
    
    #region Names

    public const string Namespace = "PureES.AggregateStores";
    
    public static string GetClassName(Aggregate aggregate) => $"{TypeNameHelpers.SanitizeName(aggregate.Type)}AggregateStore";

    public static string GetInterface(Aggregate aggregate) =>
        TypeNameHelpers.GetGenericTypeName(typeof(IAggregateStore<>), aggregate.Type.CSharpName);
    
    private string ClassName => GetClassName(_aggregate);
    private string Interface => GetInterface(_aggregate);

    private static string EventStoreType => $"global::{typeof(IEventStore).FullName}";
    private static string ServiceProviderType => $"global::{typeof(IServiceProvider).FullName}";

    private static string GetTaskType(string genericParameter) =>
        TypeNameHelpers.GetGenericTypeName(typeof(Task<>), genericParameter);

    private static string AsyncEnumerableEventEnvelope => ExternalTypes.IAsyncEnumerable($"global::{typeof(EventEnvelope)}");

    #endregion
}