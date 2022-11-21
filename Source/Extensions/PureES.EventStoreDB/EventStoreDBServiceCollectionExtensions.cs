using EventStore.Client;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.EventStore;
using PureES.EventStoreDB.Subscriptions;

// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.EventStoreDB;

public static class EventStoreDBServiceCollectionExtensions
{
    public static IServiceCollection AddEventStoreDB(this IServiceCollection services,
        Action<EventStoreDBOptions> configureOptions)
    {
        return services
            .AddSingleton(sp =>
            {
                var options = new EventStoreDBOptions();
                configureOptions(options);
                var settings = options.CreateSettings(sp);
                return new EventStoreClient(settings);
            })
            .AddTransient<IEventStore, EventStoreDBClient>();
    }

    public static IServiceCollection AddEventStoreDBSubscriptionToAll(
        this IServiceCollection services,
        Action<SubscriptionOptions>? configure = null)
    {
        return services
            .Configure<SubscriptionOptions>(nameof(SubscriptionToAll), o =>
            {
                o.SubscriptionId = "$all";
                configure?.Invoke(o);
            })
            .AddHostedService<SubscriptionToAll>();
    }
}