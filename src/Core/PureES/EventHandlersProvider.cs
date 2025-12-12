using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace PureES;

internal class EventHandlersProvider : IEventHandlersProvider
{
    private readonly IServiceProvider _services;

    public EventHandlersProvider(IServiceProvider services)
    {
        _services = services;
    }

    public IEventHandlerCollection GetHandlers(Type eventType)
    {
        var serviceType = CollectionTypes.GetOrAdd(eventType, GetCollectionType);
        return (IEventHandlerCollection)_services.GetRequiredService(serviceType);
    }

    private static Type GetCollectionType(Type eventType) =>
        typeof(EventHandlerCollection<>).MakeGenericType(eventType);

    /// <summary>
    /// Maps event types to their corresponding event handler collection types
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Type> CollectionTypes = new();
}