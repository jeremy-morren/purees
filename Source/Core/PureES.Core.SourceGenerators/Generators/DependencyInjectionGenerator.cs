using System.ComponentModel;
using PureES.Core.SourceGenerators.Framework;
using PureES.Core.SourceGenerators.Generators.Models;

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace PureES.Core.SourceGenerators.Generators;

internal class DependencyInjectionGenerator
{
    private readonly IndentedWriter _w = new();

    private readonly IReadOnlyList<Aggregate> _aggregates;
    private readonly IReadOnlyList<Models.EventHandler> _eventHandlers;

    private DependencyInjectionGenerator(IEnumerable<Aggregate> aggregates, 
        IEnumerable<Models.EventHandler> eventHandlerCollections)
    {
        _aggregates = aggregates.ToList();
        _eventHandlers = eventHandlerCollections.ToList();
    }

    public static string Generate(IEnumerable<Aggregate> aggregates, 
        IEnumerable<Models.EventHandler> eventHandlers,
        out string filename)
    {
        filename = FullClassName;
        return new DependencyInjectionGenerator(aggregates, eventHandlers).GenerateInternal();
    }

    private string GenerateInternal()
    {
        _w.WriteFileHeader(true);
        _w.WriteLine($"using {ExternalTypes.DINamespace};");
        _w.WriteLine($"using {ExternalTypes.DINamespace}.Extensions;"); //RemoveAll extension method
        _w.WriteLine();
        
        _w.WriteStatement($"namespace {Namespace}", () =>
        {
            _w.WriteClassAttributes();
            _w.WriteStatement($"internal static class {ClassName}", WriteRegisterServices);
        });
        
        return _w.Value;
    }

    private void WriteRegisterServices()
    {
        _w.WriteMethodAttributes();
        _w.WriteLine($"private static void {MethodName}(IServiceCollection services)");
        _w.PushBrace();

        _w.WriteStatement("if (services == null)", "throw new ArgumentNullException(nameof(services));");
        _w.WriteLine();

        //Get list of already registeredServices services, to reduce enumeration
        //NB: we are only interested in generic services, since both handler & store are generic
        
        var typeSet = TypeNameHelpers.GetGenericTypeName(typeof(HashSet<>), $"global::{typeof(Type).FullName}");
        _w.WriteLine($"var registeredServices = new {typeSet}();");
        _w.WriteLine($"var registeredImplementations = new {typeSet}();");
        _w.WriteStatement("foreach (var s in services)",
            () =>
            {
                _w.WriteLine("registeredServices.Add(s.ServiceType);");
                
                const string implType = "s.ImplementationType";
                
                _w.WriteStatement($"if ({implType} != null)", $"registeredImplementations.Add({implType});");
            });

        const string registeredServices = $"registeredServices.{nameof(HashSet<int>.Contains)}";
        const string registeredImplementations = $"registeredImplementations.{nameof(HashSet<int>.Contains)}";
        
        foreach (var aggregate in _aggregates)
        {
            _w.WriteLine($"// Aggregate: {aggregate.Type.FullName}. Command handlers: {aggregate.Handlers.Length}");
            
            //Factory
            var serviceType = $"typeof({AggregateFactoryGenerator.GetInterface(aggregate)})";

            AddService(serviceType,
                $"global::{AggregateFactoryGenerator.Namespace}.{AggregateFactoryGenerator.GetClassName(aggregate)}",
                true);
            
            //Store services
            if (aggregate.When.Any(w => w.Services.Any()))
            {
                var services = $"typeof(global::{AggregateFactoryGenerator.Namespace}.{AggregateFactoryGenerator.GetServicesClassName(aggregate)})";
                _w.WriteStatement($"if (!{registeredImplementations}({services}))",
                    () => AddService(services, services, false));
            }
            
            //Handlers
            foreach (var handler in aggregate.Handlers)
            {
                serviceType = $"typeof({CommandHandlerGenerator.GetInterface(handler)})";
                AddService(serviceType,
                    $"global::{CommandHandlerGenerator.Namespace}.{CommandHandlerGenerator.GetClassName(handler)}",
                    true);
            }

            _w.WriteLine();
        }
        
        _w.WriteLine($"// Event Handlers. Count: {_eventHandlers.Count}");
        foreach (var handler in _eventHandlers)
        {
            _w.WriteLine();
            var handlerType = $"typeof(global::{EventHandlerGenerator.Namespace}.{EventHandlerGenerator.GetClassName(handler)})";
            var @interface = EventHandlerGenerator.GetInterface(handler.EventType);
            _w.WriteStatement($"if (!{registeredImplementations}({handlerType}))",
                () => AddService(@interface, handlerType, false));
        }
        
        var handlerParents = _eventHandlers
            .Where(c => !c.Method.IsStatic)
            .Select(c => c.Parent)
            .Distinct()
            .ToList();
        
        _w.WriteLine($"// Event handler parents. Count: {handlerParents.Count}");
        
        //Register all parents if not already registered
        foreach (var parent in handlerParents)
        {
            _w.WriteLine();
            
            var type = $"typeof({parent.CSharpName})";
            _w.WriteStatement($"if (!{registeredServices}({type}))",
                () => AddService(type, type, false));
            
            _w.WriteLine();
        }
        
        _w.PopBrace();
    }

    private void AddService(string serviceType, string implementationType, bool replace)
    {
        if (!serviceType.StartsWith("typeof"))
            serviceType = $"typeof({serviceType})";
        if (!implementationType.StartsWith("typeof"))
            implementationType = $"typeof({implementationType})";

        if (replace)
        {
            const string registeredServices = $"registeredServices.{nameof(HashSet<int>.Contains)}";
            const string removeAll = "services.RemoveAll";
            _w.WriteStatement($"if ({registeredServices}({serviceType}))", $"{removeAll}({serviceType});");
        }
        
        _w.Write("services.Add(new ServiceDescriptor(");
        _w.WriteParameters($"serviceType: {serviceType}",
            $"implementationType: {implementationType}",
            "lifetime: ServiceLifetime.Transient");
        _w.WriteRawLine("));");
    }

    private const string Namespace = "PureES.DependencyInjection";
    private const string ClassName = "PureESServiceCollectionExtensions";
    
    public const string FullClassName = $"{Namespace}.{ClassName}";
    public const string MethodName = "Register";
}