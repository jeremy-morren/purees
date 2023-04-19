using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory.Serialization;
using PureES.EventStore.InMemory.Subscription;

namespace PureES.EventStore.InMemory;

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
        
        services.TryAddSingleton<IInMemoryEventStore, InMemoryEventStore>();

        services.TryAddTransient<IEventStore>(sp => sp.GetRequiredService<IInMemoryEventStore>());

        return services;
    }

    public static IServiceCollection AddInMemorySubscriptionToAll(this IServiceCollection services,
        Action<InMemoryEventStoreSubscriptionOptions>? configureOptions = null)
    {
        services.AddOptions<InMemoryEventStoreSubscriptionOptions>(nameof(InMemoryEventStoreSubscriptionToAll))
            .Configure(o => configureOptions?.Invoke(o));
        
        services.AddSingleton<InMemoryEventStoreSubscriptionToAll>();

        return services;
    }
}