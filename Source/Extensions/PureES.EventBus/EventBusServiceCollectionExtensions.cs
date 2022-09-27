using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable MemberCanBePrivate.Global

namespace PureES.EventBus;

public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Adds an <see cref="IEventBus"/> to the service collection
    /// </summary>
    /// <returns>
    /// The service collection so that additional calls can be chained
    /// </returns>
    public static IServiceCollection AddEventBus(this IServiceCollection services)
    {
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
        var current = services.FirstOrDefault(d => 
            d.ServiceType == typeof(Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>[]));
        if (current == null)
        {
            services.AddSingleton(new[] {factory});
        }
        else
        {
            if (current.ImplementationInstance is not Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>[] factories)
                throw new InvalidOperationException("Invalid current descriptor");
            Array.Resize(ref factories, factories.Length + 1);
            factories[^1] = factory;
            services.Remove(current);
            services.AddSingleton(factories);
        }
        //Add the composite handler
        services.TryAddSingleton<IEventHandler<TEvent, TMetadata>, CompositeEventHandler<TEvent, TMetadata>>();
        return services;
    }
}