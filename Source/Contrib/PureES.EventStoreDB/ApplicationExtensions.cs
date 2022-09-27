using EventStore.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.EventStoreDB.Subscriptions;
using Timeout = System.Threading.Timeout;

// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.EventStoreDB;

public static class ApplicationExtensions
{
    public static IServiceCollection AddEventStoreDB(this IServiceCollection services, 
        Action<EventStoreDBOptions> configureOptions)
    {
        return services
            .AddOptions()
            .Configure<EventStoreDBOptions>(configureOptions)
            .AddSingleton<EventStoreClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<EventStoreDBOptions>>();
                var settings = options.Value.CreateSettings(sp.GetRequiredService<ILoggerFactory>());
                return new EventStoreClient(settings);
            })
            .AddTransient<IEventStore, EventStoreDBClient>();
    }

    public static IServiceCollection AddEventStoreDBSubscriptionToAll(
        this IServiceCollection services,
        Action<SubscriptionOptions>? configure = null,
        bool checkpointToEventStoreDB = true)
    {
        if (checkpointToEventStoreDB)
            services.AddTransient<ISubscriptionCheckpointRepository, EventStoreSubscriptionCheckpointRepository>();
        else
            services.AddSingleton<ISubscriptionCheckpointRepository, InMemorySubscriptionCheckpointRepository>();

        return services
            .Configure<SubscriptionOptions>(o => configure?.Invoke(o))
            .AddHostedService<SubscriptionToAll>();
    }
}
