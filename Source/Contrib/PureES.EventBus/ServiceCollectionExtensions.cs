using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using PureES.Core;

// ReSharper disable MemberCanBePrivate.Global

namespace PureES.EventBus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventBus(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventBus, EventBus>();
        return services;
    }

    /// <summary>
    /// Adds an EventHandler to the service collection
    /// </summary>
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
            var factories = (Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>[]) current.ImplementationInstance;
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