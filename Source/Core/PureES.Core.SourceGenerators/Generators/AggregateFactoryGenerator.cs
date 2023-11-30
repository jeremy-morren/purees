using PureES.Core.SourceGenerators.Framework;
using PureES.Core.SourceGenerators.Generators.Models;

namespace PureES.Core.SourceGenerators.Generators;

internal class AggregateFactoryGenerator
{
    /*
     * We have to generate an aggregate store, and a _services class
     * This is because some aggregate stores depend on each other
     * therefore we cannot inject services into the constructor
     * Rather, we use GetRequiredService when actually handling
     */
    
    public static string Generate(Aggregate aggregate, out string filename)
    {
        filename = $"{Namespace}.{GetClassName(aggregate)}";
        return new AggregateFactoryGenerator(aggregate).GenerateInternal();
    }

    private readonly IndentedWriter _w = new();

    private readonly Aggregate _aggregate;
    private readonly IType[] _services;
    
    private AggregateFactoryGenerator(Aggregate aggregate)
    {
        _aggregate = aggregate;
        _services = aggregate.When.SelectMany(w => w.Services).Distinct().ToArray();
    }

    private string GenerateInternal()
    {
        _w.WriteFileHeader(false);

        _w.WriteLine();
        
        _w.WriteLine($"namespace {Namespace}");
        _w.PushBrace();

        _w.WriteClassAttributes();
        _w.WriteLine($"internal sealed class {ClassName} : {Interface}");
        _w.PushBrace();
        
        WriteConstructor();
        
        _w.WriteLine($"private static readonly global::{typeof(Type).FullName} AggregateType = typeof({_aggregate.Type.CSharpName});");
        
        WriteFactories();
        
        if (_services.Any())
            WriteServicesClass();
        
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

    private void WriteFactories()
    {
        var services = _services.Any() ? $"{ServicesClassName} services, " : null;
        var servicesParam =  _services.Any() ? "services, " : null;
        
        const string moveNext = "await enumerator.MoveNextAsync()";
        
        //So that we can generate the create factory, we have to implement the MoveNext methods manually
        
        //Create when
        _w.WriteMethodAttributes();
        _w.WriteStatement(
            $"private async Task<{RehydratedAggregate}> CreateWhen(string streamId, {AsyncEnumeratorEventEnvelope} enumerator, {services}CancellationToken ct)",
            () =>
            {
                //Get the first item, or throw
                _w.WriteStatement($"if (!{moveNext})",
                    "throw new ArgumentException(\"Stream is empty\");");
                
                _w.WriteLine($"{_aggregate.Type.CSharpName} current;");
                
                WriteWhenSwitch(_aggregate.When.Where(w => !w.IsUpdate).ToList(), "CreateWhen");
                
                _w.WriteLine($"return new {RehydratedAggregate}(current, 0ul);");
            });
        
        //Update when
        _w.WriteMethodAttributes();
        _w.WriteStatement(
            $"private async Task<{RehydratedAggregate}> UpdateWhen(string streamId, {RehydratedAggregate} aggregate, {AsyncEnumeratorEventEnvelope} enumerator, {services}CancellationToken ct)",
            () =>
            {
                _w.WriteLine($"{_aggregate.Type.CSharpName} current = aggregate.Aggregate;");
                _w.WriteLine("var revision = aggregate.StreamPosition;");
                _w.WriteStatement($"while ({moveNext})", () =>
                {
                    WriteWhenSwitch(_aggregate.When.Where(w => w.IsUpdate).ToList(), "UpdateWhen");
                    _w.WriteLine("++revision;");
                });
                _w.WriteLine($"return new {RehydratedAggregate}(current, revision);");
            });
        
        //Wrapping implementations (CreateAsync/UpdateAsync)
        
        _w.WriteMethodAttributes();
        _w.WriteStatement($"public async Task<{RehydratedAggregate}> {nameof(IAggregateFactory<object>.Create)}(string streamId, {AsyncEnumerableEventEnvelope} @events, CancellationToken cancellationToken)",
            () =>
            {
                if (_services.Any())
                    _w.WriteLine($"var services = this._services.GetRequiredService<{ServicesClassName}>();");
                _w.WriteStatement("await using (var enumerator = @events.GetAsyncEnumerator(cancellationToken))", () =>
                {
                    _w.WriteLine($"var current = await CreateWhen(streamId, enumerator, {servicesParam}cancellationToken);");
                    _w.WriteLine($"return await UpdateWhen(streamId, current, enumerator, {servicesParam}cancellationToken);");
                });
            });
        
        _w.WriteMethodAttributes();
        _w.WriteStatement($"public async Task<{RehydratedAggregate}> {nameof(IAggregateFactory<object>.Update)}(string streamId, {RehydratedAggregate} aggregate, {AsyncEnumerableEventEnvelope} @events, CancellationToken cancellationToken)",
            () =>
            {
                if (_services.Any())
                    _w.WriteLine($"var services = this._services.GetRequiredService<{ServicesClassName}>();");
            
                _w.WriteStatement("await using (var enumerator = @events.GetAsyncEnumerator(cancellationToken))", () =>
                {
                    _w.WriteLine(
                        $"return await UpdateWhen(streamId, aggregate, enumerator, {servicesParam}cancellationToken);");
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
                //Fallthrough error
                _w.WriteLine($"var eventType = {GetTypeName}(enumerator.Current.Event.GetType());");
                ThrowRehydrationException($"$\"No suitable {fallthroughName} method found for event '{{eventType}}'\"");
            });
        });
        
        //catch-all handlers
        foreach (var w in _aggregate.When.Where(w => w.Event == null))
            InvokeWhen(w);
    }

    private void ThrowRehydrationException(string parameters)
    {
        var type = $"global::{typeof(RehydrationException).FullName}";
        _w.WriteLine($"throw new {type}(streamId, AggregateType, {parameters});");
    }

    private void InvokeWhen(When when)
    {
        _w.WriteStatement("try", () =>
        {
            var @await = when.IsAsync ? "await " : string.Empty;

            _w.Write(when.Method.IsStatic
                ? $"current = {@await}{_aggregate.Type.CSharpName}.{when.Method.Name}("
                : $"{@await}current.{when.Method.Name}(");

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
                    return $"services.S{_services.GetIndex(p.Type)}";
            
                if (p.Type.Equals(_aggregate.Type))
                    return "current"; //Provide current aggregate parameter

                if (p.Type.IsCancellationToken())
                    return "ct";
            
                throw new NotImplementedException("Unknown parameter");
            });

            _w.WriteParameters(parameters);

            _w.WriteRawLine(");");
        });
        _w.WriteStatement("catch (Exception ex)", () =>
        {
            ThrowRehydrationException($"\"{when.Method}\", ex");
        });
    }

    private void WriteServicesClass()
    {
        _w.WriteLine();
        _w.WriteClassAttributes();
        _w.WriteLine("internal sealed class Services");
        _w.PushBrace();
        for (var i = 0; i < _services.Length; i++)
            _w.WriteLine($"public readonly {_services[i].CSharpName} S{i};");

        _w.WriteLine();
        
        _w.WriteMethodAttributes();
        _w.Write("public Services(");
        _w.WriteParameters(_services.Select((t,i) => $"{t.CSharpName} s{i}"));
        _w.WriteRawLine(")");
        _w.PushBrace();
        for (var i = 0; i < _services.Length; i++)
            _w.WriteLine($"this.S{i} = s{i};");
        _w.PopBrace(); //Constructor
        
        _w.PopBrace(); //Class
    }
    
    #region Names

    public const string Namespace = "PureES.AggregateFactories";
    
    public static string GetClassName(Aggregate aggregate) => $"{TypeNameHelpers.SanitizeName(aggregate.Type)}Factory";

    public static string GetServicesClassName(Aggregate aggregate) => $"{GetClassName(aggregate)}.Services";

    public static string GetInterface(Aggregate aggregate) =>
        TypeNameHelpers.GetGenericTypeName(typeof(IAggregateFactory<>), aggregate.Type.CSharpName);
    
    private string ClassName => GetClassName(_aggregate);
    private string ServicesClassName => GetServicesClassName(_aggregate);
    private string Interface => GetInterface(_aggregate);

    private string RehydratedAggregate =>
        TypeNameHelpers.GetGenericTypeName(typeof(RehydratedAggregate<>), _aggregate.Type.CSharpName);
    
    private static string GetTypeName => $"global::{typeof(BasicEventTypeMap).FullName}.{nameof(BasicEventTypeMap.GetTypeName)}";

    private static string EventStoreType => $"global::{typeof(IEventStore).FullName}";
    private static string ServiceProviderType => $"global::{typeof(IServiceProvider).FullName}";

    private static string AsyncEnumerableEventEnvelope => ExternalTypes.IAsyncEnumerable($"global::{typeof(EventEnvelope)}");
    private static string AsyncEnumeratorEventEnvelope => ExternalTypes.IAsyncEnumerator($"global::{typeof(EventEnvelope)}");

    #endregion
}