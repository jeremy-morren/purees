using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PureES.EventStore.EFCore.Models;
using PureES.EventStore.EFCore.Subscriptions;

namespace PureES.EventStore.EFCore;

[PublicAPI]
public static class EfCoreEventStoreServiceCollectionExtensions
{
    /// <summary>
    /// Add an event store using EFCore
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional delegate to configure event store options</param>
    /// <typeparam name="TContext">The source <see cref="DbContext"/> whose options should be used</typeparam>
    /// <returns></returns>
    public static PureESEfCoreBuilder AddEfCoreEventStore<TContext>(
        this IServiceCollection services,
        Action<EfCoreEventStoreOptions>? configureOptions = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        
        services.AddOptions<EfCoreEventStoreOptions>()
            .Configure(configureOptions ?? (_ => { }));

        services.AddTransient<EventStoreDbContext<TContext>>();
        services.AddTransient<EfCoreEventSerializer>();
        
        services.AddTransient<IEventStore, EfCoreEventStore<TContext>>();
        services.AddTransient<IEfCoreEventStore, EfCoreEventStore<TContext>>();

        return new PureESEfCoreBuilder(services);
    }
}

public class PureESEfCoreBuilder
{
    private readonly IServiceCollection _services;

    public PureESEfCoreBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Adds a subscription to all events
    /// </summary>
    public PureESEfCoreBuilder AddSubscriptionToAll()
    {
        _services.AddHostedService<EfCoreEventStoreSubscriptionToAll>();

        return this;
    }
}