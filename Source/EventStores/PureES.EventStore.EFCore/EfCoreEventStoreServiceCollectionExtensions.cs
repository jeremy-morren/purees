using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using PureES.EventStore.EFCore.Subscriptions;

namespace PureES.EventStore.EFCore;

/// <summary>
/// Methods to simplify adding EFCore event store to a service collection
/// </summary>
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
            .Configure(configureOptions ?? (_ => { }))
            .Validate(o => o.Validate())
            .PostConfigure(o => o.PostConfigure());

        services.AddTransient<EfCoreEventSerializer>();

        services.AddTransient<IEventStore, EfCoreEventStore<TContext>>();
        services.AddTransient<IEfCoreEventStore, EfCoreEventStore<TContext>>();

        services.AddDbContextFactory<EventStoreDbContext<TContext>>();

        return new PureESEfCoreBuilder(services);
    }


    /// <summary>
    /// Adds a subscription to all events
    /// </summary>
    public static PureESEfCoreBuilder AddSubscriptionToAll(this PureESEfCoreBuilder builder)
    {
        builder.Services.AddHostedService<EfCoreEventStoreSubscriptionToAll>();

        return builder;
    }

    /// <summary>
    /// Configures the event store options
    /// </summary>
    public static PureESEfCoreBuilder Configure(this PureESEfCoreBuilder builder, Action<EfCoreEventStoreOptions> optionsAction)
    {
        ArgumentNullException.ThrowIfNull(optionsAction);
        builder.Services.Configure(optionsAction);
        return builder;
    }

    /// <summary>
    /// Configures the event store options
    /// </summary>
    public static PureESEfCoreBuilder Configure(this PureESEfCoreBuilder builder, Action<EfCoreEventStoreOptions, IServiceProvider> optionsAction)
    {
        ArgumentNullException.ThrowIfNull(optionsAction);
        builder.Services.AddOptions<EfCoreEventStoreOptions>().Configure(optionsAction);
        return builder;
    }
}

/// <summary>
/// A builder for configuring EFCore event store
/// </summary>
[PublicAPI]
public class PureESEfCoreBuilder
{
    public IServiceCollection Services { get; }

    public PureESEfCoreBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }
}