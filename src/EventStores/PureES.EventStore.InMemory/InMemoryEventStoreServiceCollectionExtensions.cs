using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public static InMemoryEventStoreBuilder AddInMemoryEventStore(this IServiceCollection services,
        Action<InMemoryEventStoreOptions>? configureOptions = null)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<InMemoryEventStoreOptions>()
            .Configure(o => configureOptions?.Invoke(o))
            .Validate(o => o.Validate())
            .PostConfigure(o => o.PostConfigure());
        
        services.TryAddSingleton<InMemoryEventStoreSerializer>();
        
        services.TryAddSingleton<IEventStore, InMemoryEventStore>();

        services.TryAddSingleton<IInMemoryEventStore>(sp => (IInMemoryEventStore)sp.GetRequiredService<IEventStore>());

        return new InMemoryEventStoreBuilder(services);
    }

    public static IServiceCollection AddInMemorySubscriptionToAll(this IServiceCollection services)
    {
        if (services.All(x => x.ImplementationType != typeof(InMemoryEventStoreSubscriptionToAll)))
            services.AddHostedService<InMemoryEventStoreSubscriptionToAll>();

        return services;
    }
}

public class InMemoryEventStoreBuilder
{
    public IServiceCollection Services { get; }

    public InMemoryEventStoreBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public InMemoryEventStoreBuilder AddSubscriptionToAll()
    {
        Services.AddInMemorySubscriptionToAll();
        return this;
    }

    public InMemoryEventStoreBuilder Configure(Action<InMemoryEventStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.Configure(configure);
        return this;
    }

    public InMemoryEventStoreBuilder Configure(Action<InMemoryEventStoreOptions, IServiceProvider> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.AddOptions<InMemoryEventStoreOptions>().Configure(configure);
        return this;
    }
}