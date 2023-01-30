using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable MemberCanBePrivate.Global

namespace PureES.EventBus;

public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    ///     Adds an <see cref="IEventBus" /> to the service collection
    /// </summary>
    /// <returns>
    ///     The service collection so that additional calls can be chained
    /// </returns>
    public static IServiceCollection AddEventBus(this IServiceCollection services,
        Action<EventBusOptions>? configureOptions = null)
    {
        services.Configure<EventBusOptions>(o => configureOptions?.Invoke(o));
        
        services.TryAddSingleton<IEventBus, EventBus>();

        return services;
    }

    /// <summary>
    ///     Adds an event handler for event <typeparamref name="TEvent" />
    /// </summary>
    /// <returns>
    ///     The service collection so that additional calls can be chained
    /// </returns>
    public static IServiceCollection AddEventHandler<TEvent, TMetadata>(this IServiceCollection services,
        Func<IServiceProvider, IEventHandler<TEvent, TMetadata>> factory)
        where TEvent : notnull
        where TMetadata : notnull
    {
        services.AddSingleton<Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>>(factory);
        return services;
    }
}