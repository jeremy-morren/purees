using PureES.SourceGenerators.Framework;
using PureES.SourceGenerators.Models;
using EventHandler = PureES.SourceGenerators.Models.EventHandler;

namespace PureES.SourceGenerators;

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
        
        //Allow marking when methods as obsolete, for use with events that are marked as obsolete
        _w.WriteLine("#pragma warning disable CS0612 // Type or member is obsolete");
        _w.WriteLine("#pragma warning disable CS0618 // Type or member is obsolete");

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
        if (_services.Length > 0)
            _w.WriteLine($"private readonly {ServiceProviderType} _serviceProvider;");
        
        _w.WriteMethodAttributes();
        _w.Write($"public {ClassName}(");

        if (_services.Length > 0)
            _w.WriteParameters($"{ServiceProviderType} serviceProvider");

        _w.WriteRawLine(")");
        _w.PushBrace();

        if (_services.Length > 0)
            _w.WriteLine("this._serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));");
        
        _w.PopBrace();
    }

    private void WriteFactories()
    {
        string? createServices = null;
        if (_services.Length > 0)
        {
            _w.WriteLine($"private {ServicesClassName} _services;");
            createServices = $"_services ??= _serviceProvider.GetRequiredService<{ServicesClassName}>();";
        }

        var createWhen = _aggregate.When.Where(w => !w.IsUpdate).ToList();
        var updateWhen = _aggregate.When.Where(w => w.IsUpdate).ToList();

        var createIsAsync = IsAsync(createWhen);
        var updateIsAsync = IsAsync(updateWhen);

        //Create when
        _w.WriteMethodAttributes();
        _w.WriteStatement(
            $"public {(createIsAsync ? "async " : "")}ValueTask<{_aggregate.Type.CSharpName}> CreateWhen({EventEnvelope} envelope, CancellationToken cancellationToken)",
            () =>
            {
                _w.CheckNotNull("envelope");
                if (createServices != null)
                    _w.WriteLine(createServices);
                _w.WriteLine($"{_aggregate.Type.CSharpName} current;");
                WriteWhenSwitch(createWhen, "CreateWhen");

                _w.WriteLine(createIsAsync ? "return current;" : "return ValueTask.FromResult(current);");
            });
        
        //Update when
        _w.WriteMethodAttributes();
        _w.WriteStatement(
            $"public {(updateIsAsync ? "async " : "")}ValueTask<{_aggregate.Type.CSharpName}> UpdateWhen({EventEnvelope} envelope, {_aggregate.Type.CSharpName} current, CancellationToken cancellationToken)",
            () =>
            {
                _w.CheckNotNull("envelope");
                if (createServices != null)
                    _w.WriteLine(createServices);
                WriteWhenSwitch(updateWhen, "UpdateWhen");
                _w.WriteLine(updateIsAsync ? "return current;" : "return ValueTask.FromResult(current);");
            });
    }

    private void WriteWhenSwitch(IEnumerable<When> source, string fallthroughName)
    {
        //Sort by inheritance depth descending, so that derived events are placed before base events in the switch statement
        source = source.OrderByDescending(w => w.GetInheritanceDepth()).ToList();
        _w.WriteStatement("switch (envelope.Event)", () =>
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
                _w.WriteLine($"var eventType = {GetTypeName}(envelope.Event.GetType());");
                ThrowRehydrationException($"$\"No suitable {fallthroughName} method found for event '{{eventType}}'\"");
            });
        });
        
        //catch-all handlers
        foreach (var w in _aggregate.When.Where(w => w.Event == null))
            InvokeWhen(w);
    }

    private void ThrowRehydrationException(string parameters)
    {
        _w.WriteLine($"throw new global::{PureESSymbols.RehydrationException}(envelope, AggregateType, {parameters});");
    }

    private void InvokeWhen(When when)
    {
        _w.WriteStatement("try", () =>
        {
            var await = when.IsAsync ? "await " : string.Empty;

            _w.Write(when.Method.IsStatic
                ? $"current = {await}{_aggregate.Type.CSharpName}.{when.Method.Name}("
                : $"{await}current.{when.Method.Name}(");

            var parameters = when.Method.Parameters.Select(p =>
            {
                if (p.Type.IsNonGenericEventEnvelope())
                    return "envelope";
                if (p.HasEventAttribute())
                {
                    if (when.Event == null || !p.Type.Equals(when.Event))
                        throw new NotImplementedException();
                    return "e"; //This is a local variable from switch statement
                }
                if (p.Type.IsGenericEventEnvelope(out var e, out _))
                {
                    if (when.Event == null || !e.Equals(when.Event))
                        throw new NotImplementedException();
                    return $"new {p.Type.CSharpName}(envelope)"; //Create envelope from non-generic envelope current
                }

                if (p.HasFromServicesAttribute())
                    return $"_services.S{_services.GetIndex(p.Type)}";
            
                if (p.Type.Equals(_aggregate.Type))
                    return "current"; //Provide current aggregate parameter

                if (p.Type.IsCancellationToken())
                    return "cancellationToken";
            
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

    private static bool IsAsync(IEnumerable<When> handlers) => handlers.Any(h => h.IsAsync);
    
    #region Names

    public const string Namespace = "PureES.AggregateFactories";
    
    public static string GetClassName(Aggregate aggregate) => $"{TypeNameHelpers.SanitizeName(aggregate.Type)}Factory";

    public static string GetServicesClassName(Aggregate aggregate) => $"{GetClassName(aggregate)}.Services";

    public static string GetInterface(Aggregate aggregate) =>
        $"global::{PureESSymbols.IAggregateFactory}<{aggregate.Type.CSharpName}>";
    
    private string ClassName => GetClassName(_aggregate);
    private string ServicesClassName => GetServicesClassName(_aggregate);
    private string Interface => GetInterface(_aggregate);

    private static string GetTypeName => $"global::{PureESSymbols.BasicEventTypeMap}.GetTypeName";
    private static string ServiceProviderType => $"global::{typeof(IServiceProvider).FullName}";

    private const string EventEnvelope = $"global::{PureESSymbols.EventEnvelope}";

    #endregion
}