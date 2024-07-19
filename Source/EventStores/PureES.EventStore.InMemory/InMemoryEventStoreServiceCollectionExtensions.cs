using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using PureES.EventStore.InMemory.Subscription;

namespace PureES.EventStore.InMemory;

[PublicAPI]
public static class InMemoryEventStoreServiceCollectionExtensions
{
    /// <summary>
    ///     Adds an <see cref="IEventStore" /> implementation
    ///     that persists events in Memory.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configureOptions">Configure in-memory event store options</param>
    /// <returns>The service collection, so that further calls can be changed</returns>
    /// <remarks>
    ///     Any existing registrations of <see cref="IEventStore" /> will not be overwritten,
    ///     hence this method is safe to be called multiple times
    /// </remarks>
    public static IServiceCollection AddInMemoryEventStore(this IServiceCollection services,
        Action<InMemoryEventStoreOptions>? configureOptions = null)
    {
        services.TryAddSingleton<ISystemClock, SystemClock>();

        services.AddOptions<InMemoryEventStoreOptions>()
            .Configure(o => configureOptions?.Invoke(o))
            .Validate(o =>
            {
                o.Validate();
                return true;
            });
        
        services.TryAddSingleton<InMemoryEventStoreSerializer>();
        
        services.TryAddSingleton<IEventStore, InMemoryEventStore>();

        services.TryAddSingleton<IInMemoryEventStore>(sp => (IInMemoryEventStore)sp.GetRequiredService<IEventStore>());

        return services;
    }

    public static IServiceCollection AddInMemorySubscriptionToAll(this IServiceCollection services)
    {
        services.AddHostedService<InMemoryEventStoreSubscriptionToAll>();

        return services;
    }
}