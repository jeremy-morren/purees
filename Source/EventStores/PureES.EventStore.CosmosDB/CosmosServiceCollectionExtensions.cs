using JetBrains.Annotations;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using PureES.EventStore.CosmosDB.Serialization;
using PureES.EventStore.CosmosDB.Subscriptions;
using PureES.EventStore.CosmosDB.Telemetry;

namespace PureES.EventStore.CosmosDB;

[PublicAPI]
public static class CosmosServiceCollectionExtensions
{
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

        services.AddTransient<ITelemetryInitializer, CosmosEventStoreSubscriptionTelemetryInitializer>();
        services.AddApplicationInsightsTelemetryProcessor<CosmosEventStoreSubscriptionTelemetryProcessor>();

        return services;
    }

    public static IServiceCollection AddCosmosEventStoreSubscriptionToAll(this IServiceCollection services,
        Action<CosmosEventStoreSubscriptionOptions>? configureOptions = null)
    {
        services.AddOptions<CosmosEventStoreSubscriptionOptions>(nameof(CosmosEventStoreSubscriptionToAll))
            .Configure(o => configureOptions?.Invoke(o));
        
        return services.AddHostedService<CosmosEventStoreSubscriptionToAll>();
    }
}