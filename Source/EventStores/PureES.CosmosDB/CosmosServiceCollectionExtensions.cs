using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using PureES.Core.EventStore;
using PureES.CosmosDB.Serialization;
using PureES.CosmosDB.Subscription;

namespace PureES.CosmosDB;

public static class CosmosServiceCollectionExtensions
{
    public static IServiceCollection AddCosmosEventStore(this IServiceCollection services,
        Action<IServiceProvider, CosmosEventStoreOptions> configureOptions)
    {
        services.TryAddSingleton<ISystemClock, SystemClock>();
        
        services.AddTransient<CosmosEventStoreSerializer>();
        services.AddTransient<ICosmosEventStoreSerializer, CosmosEventStoreSerializer>();

        services.AddSingleton<CosmosEventStoreClient>();

        services.AddSingleton<IEventStore, CosmosEventStore>();
        
        services.AddOptions<CosmosEventStoreOptions>()
            .Validate(o =>
            {
                o.Validate();
                return true;
            });

        services.AddSingleton<IConfigureOptions<CosmosEventStoreOptions>>(sp =>
            new ConfigureOptions<CosmosEventStoreOptions>(o => configureOptions(sp, o)));

        services.AddHttpClient(CosmosEventStoreClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CosmosEventStoreOptions>>().Value;

                var handler = new SocketsHttpHandler();
                if (!options.VerifyTLSCert)
                    handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                
                return handler;
            });

        return services;
    }

    public static IServiceCollection AddCosmosEventStore(this IServiceCollection services,
        Action<CosmosEventStoreOptions> configureOptions) =>
        AddCosmosEventStore(services, (_, o) => configureOptions(o));

    public static IServiceCollection AddCosmosEventStoreSubscriptionToAll(this IServiceCollection services,
        Action<CosmosEventStoreSubscriptionOptions>? configureOptions = null)
    {
        services.AddOptions<CosmosEventStoreSubscriptionOptions>(nameof(CosmosEventStoreSubscriptionToAll))
            .Configure(o => configureOptions?.Invoke(o));
        
        return services.AddHostedService<CosmosEventStoreSubscriptionToAll>();
    }
}