using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable MemberCanBePrivate.Global

namespace PureES.EventBus;

public static class EventBusServiceCollectionExtensions
{
    private static void AddEventHandlersCore(this IServiceCollection services,
        Action<EventBusOptions>? configureOptions)
    {
        services.TryAddSingleton(new EventHandlerCollection());
        services.TryAddTransient(typeof(IEventHandler<,>), typeof(CompositeEventHandler<,>));
        services.Configure<EventBusOptions>(o => configureOptions?.Invoke(o));
    }
    
    /// <summary>
    /// Adds an <see cref="IEventBus"/> to the service collection
    /// </summary>
    /// <returns>
    /// The service collection so that additional calls can be chained
    /// </returns>
    public static IServiceCollection AddEventBus(this IServiceCollection services, 
        Action<EventBusOptions>? configureOptions = null)
    {
        services.AddEventHandlersCore(configureOptions);
        
        services.TryAddSingleton<IEventBus, EventBus>();
        
        return services;
    }

    /// <summary>
    /// Adds an event handler for event <typeparamref name="TEvent"/>
    /// </summary>
    /// <returns>
    /// The service collection so that additional calls can be chained
    /// </returns>
    public static IServiceCollection AddEventHandler<TEvent, TMetadata>(this IServiceCollection services,
        Func<IServiceProvider, IEventHandler<TEvent, TMetadata>> factory)
        where TEvent : notnull
        where TMetadata : notnull
    {
        services.AddEventHandlersCore(null);

        var descriptor = services.Single(d => d.ServiceType == typeof(EventHandlerCollection));

        var collection = (EventHandlerCollection?) descriptor.ImplementationInstance!;
        collection.AddEventHandler(factory);
        return services;
    }
}