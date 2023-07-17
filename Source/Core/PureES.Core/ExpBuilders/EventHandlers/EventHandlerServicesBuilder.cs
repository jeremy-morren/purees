using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PureES.Core.ExpBuilders.EventHandlers;

public class EventHandlerServicesBuilder
{
    private readonly PureESOptions _options;

    public EventHandlerServicesBuilder(PureESOptions options) => _options = options;
    
    private IEnumerable<MethodInfo> GetEventHandlers() =>
        _options.Assemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            .Where(m => m.GetCustomAttribute(typeof(EventHandlerAttribute)) != null);

    public void AddEventHandlerServices(IServiceCollection services)
    {
        var wrappingTypes = GetEventHandlers()
            .Where(m => !m.IsStatic)
            .Select(m => m.DeclaringType!)
            .Where(t => t != null!)
            .Distinct();

        //For all types which aren't already registered, add them as a Transient Dependency
        foreach (var wrappingType in wrappingTypes)
            services.TryAddTransient(wrappingType);
    }
    
    public Dictionary<Type, EventHandlerDelegate[]> BuildEventHandlers()
    {
        var builder = new EventHandlerExpBuilder(_options.BuilderOptions);

        var handlers = new Dictionary<Type, EventHandlerDelegate[]>();
    
        foreach (var method in GetEventHandlers())
        {
            var func = builder.BuildEventHandlerFactory(method).Compile();
            var name = $"{method.DeclaringType?.FullName}+{method.Name}";
            var type = GetEventType(method);
            if (handlers.TryGetValue(type, out var array))
            {
                Array.Resize(ref array, array.Length + 1);
                array[^1] = new EventHandlerDelegate(name, func);
                handlers[type] = array;
            }
            else
            {
                handlers.Add(GetEventType(method), new[] {new EventHandlerDelegate(name, func)});
            }
        }

        return handlers;
    }

    private Type GetEventType(MethodBase method)
    {
        var builder = new EventHandlerExpBuilder(_options.BuilderOptions);
        var type = method.GetParameters()
            .Single(p => builder.IsEventEnvelope(p.ParameterType))
            .ParameterType;
        return _options.BuilderOptions.GetEventType?.Invoke(type) ?? type.GetGenericArguments()[0];
    }
    
}