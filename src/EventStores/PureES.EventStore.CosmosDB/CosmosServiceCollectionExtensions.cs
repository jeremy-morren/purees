using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using PureES.EventStore.CosmosDB.Serialization;
using PureES.EventStore.CosmosDB.Subscriptions;

namespace PureES.EventStore.CosmosDB;

[PublicAPI]
public static class CosmosServiceCollectionExtensions
{
    /// <summary>
    /// Adds an event store that persists events to Azure Cosmos DB.
    /// </summary>
    public static IServiceCollection AddCosmosEventStore(this IServiceCollection services,
        Action<CosmosEventStoreOptions> configureOptions)
    {
        services.TryAddSingleton<ISystemClock, SystemClock>();

        services.AddTransient<CosmosEventStoreSerializer>();

        services.AddSingleton<CosmosEventStoreClient>();

        services.AddSingleton<IEventStore, CosmosEventStore>();

        services.AddOptions<CosmosEventStoreOptions>()
            .Configure(configureOptions)
            .Validate(o =>
            {
                o.Validate();
                return true;
            });

        services.AddHttpClient(CosmosEventStoreClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CosmosEventStoreOptions>>().Value;

                var handler = new SocketsHttpHandler();
                if (options.Insecure)
                    handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

                return handler;
            });

        return services;
    }

    /// <summary>
    /// Adds a Cosmos DB Event Store subscription that listens to all events.
    /// </summary>
    public static IServiceCollection AddCosmosEventStoreSubscriptionToAll(this IServiceCollection services,
        Action<CosmosEventStoreSubscriptionOptions>? configureOptions = null)
    {
        services.AddOptions<CosmosEventStoreSubscriptionOptions>(nameof(CosmosEventStoreSubscriptionToAll))
            .Configure(o => configureOptions?.Invoke(o));

        return services.AddHostedService<CosmosEventStoreSubscriptionToAll>();
    }
}