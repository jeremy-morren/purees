using EventStore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PureES.Core.EventStore;
using PureES.EventStoreDB.Subscriptions;

// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.EventStoreDB;

public static class EventStoreDBServiceCollectionExtensions
{
    public static IServiceCollection AddEventStoreDB(this IServiceCollection services,
        Action<EventStoreDBOptions> configureOptions)
    {
        services.AddOptions<EventStoreDBOptions>()
            .Configure(configureOptions)
            .Validate(o =>
            {
                o.Validate();
                _ = o.CreateSettings(NullLoggerFactory.Instance);
                return true;
            });
        
        return services
            .AddSingleton<EventStoreClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<EventStoreDBOptions>>();
                var loggerFactory = sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                var settings = options.Value.CreateSettings(loggerFactory);
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