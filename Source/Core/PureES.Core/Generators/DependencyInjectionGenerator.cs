﻿using System.ComponentModel;
using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace PureES.Core.Generators;

internal class DependencyInjectionGenerator
{
    private readonly IndentedWriter _w = new();

    private readonly IReadOnlyList<Aggregate> _aggregates;
    private readonly IReadOnlyList<EventHandlerCollection> _eventHandlerCollections;

    private DependencyInjectionGenerator(IEnumerable<Aggregate> aggregates, 
        IEnumerable<EventHandlerCollection> eventHandlerCollections)
    {
        _aggregates = aggregates.ToList();
        _eventHandlerCollections = eventHandlerCollections.ToList();
    }

    public static string Generate(IEnumerable<Aggregate> aggregates, 
        IEnumerable<EventHandlerCollection> eventHandlerCollections,
        out string filename)
    {
        filename = FullClassName;
        return new DependencyInjectionGenerator(aggregates, eventHandlerCollections).GenerateInternal();
    }

    private string GenerateInternal()
    {
        _w.WriteFileHeader(true);
        _w.WriteLine($"using {ExternalTypes.DINamespace};");
        _w.WriteLine($"using {ExternalTypes.DINamespace}.Extensions;"); //RemoveAll extension method
        _w.WriteLine();
        
        _w.WriteStatement($"namespace {Namespace}", () =>
        {
            _w.WriteClassAttributes(EditorBrowsableState.Never);
            _w.WriteStatement($"internal class {ClassName}", WriteRegisterServices);
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

        const string removeAll = "services.RemoveAll";
        
        foreach (var aggregate in _aggregates)
        {
            _w.WriteLine($"// Aggregate: {aggregate.Type.FullName}. Command handlers: {aggregate.Handlers.Length}");
            
            //Store
            var serviceType = $"typeof({AggregateStoreGenerator.GetInterface(aggregate)})";
            
            //Remove existing if found
            _w.WriteStatement($"if ({registeredServices}({serviceType}))", $"{removeAll}({serviceType});");
            AddService(serviceType,
                $"global::{AggregateStoreGenerator.Namespace}.{AggregateStoreGenerator.GetClassName(aggregate)}");
            
            //Handlers
            foreach (var handler in aggregate.Handlers)
            {
                serviceType = $"typeof({CommandHandlerGenerator.GetInterface(handler)})";
                _w.WriteStatement($"if ({registeredServices}({serviceType}))", $"{removeAll}({serviceType});");
                AddService(serviceType,
                    $"global::{CommandHandlerGenerator.Namespace}.{CommandHandlerGenerator.GetClassName(handler)}");
            }

            _w.WriteLine();
        }
        
        _w.WriteLine($"// Event Handlers. Count: {_eventHandlerCollections.Count}");
        foreach (var collection in _eventHandlerCollections)
        {
            _w.WriteLine();
            var handlerType = $"typeof(global::{EventHandlerGenerator.Namespace}.{EventHandlerGenerator.GetClassName(collection.EventType)})";
            var @interface = EventHandlerGenerator.GetInterface(collection.EventType);
            _w.WriteStatement($"if (!{registeredImplementations}({handlerType}))",
                () => AddService(@interface, handlerType));
        }

        
        var parents = _eventHandlerCollections.SelectMany(c => c.Parents)
            .Distinct()
            .ToList();
        
        _w.WriteLine($"// Event handler parents. Count: {parents.Count}");
        
        //Register all parents if not already registered
        foreach (var parent in parents)
        {
            _w.WriteLine();
            
            var type = $"typeof({parent.CSharpName})";
            _w.WriteStatement($"if (!{registeredServices}({type}))",
                () => AddService(type, type));
            
            _w.WriteLine();
        }
        
        _w.PopBrace();
    }

    private void AddService(string serviceType, string implementationType)
    {
        if (!serviceType.StartsWith("typeof"))
            serviceType = $"typeof({serviceType})";
        if (!implementationType.StartsWith("typeof"))
            implementationType = $"typeof({implementationType})";
        
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