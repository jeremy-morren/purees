using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore;

[PublicAPI]
public static class EfCoreEventStoreServiceCollectionExtensions
{
    /// <summary>
    /// Add an event store using EFCore
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional delegate to configure event store options</param>
    /// <typeparam name="TContext">The <see cref="DbContext"/> whose options should be used</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEfCoreEventStore<TContext>(
        this IServiceCollection services,
        Action<EfCoreEventStoreOptions>? configureOptions = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        
        services.AddOptions<EfCoreEventStoreOptions>()
            .Configure(configureOptions ?? (_ => { }));

        services.AddTransient<EventStoreDbContext<TContext>>();
        services.AddTransient<IEventStore, EfCoreEventStore<TContext>>();
        services.AddTransient<EfCoreEventSerializer>();
        
        return services;
    }
}