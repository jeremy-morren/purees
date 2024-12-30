using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PureES.EventStore.CosmosDB.Subscriptions;

namespace PureES.EventStore.CosmosDB;

[PublicAPI]
public static class CosmosEventStoreSetup
{
    public static async Task InitializeEventStore(IServiceProvider services, CancellationToken ct)
    {
        var svc = services.GetRequiredService<CosmosEventStoreClient>();
        
        await svc.InitializeEventStoreContainer(ct);

        var hostedServices = services.GetService<IEnumerable<IHostedService>>() ?? Enumerable.Empty<IHostedService>();
        
        if (hostedServices.OfType<ICosmosEventStoreSubscription>().Any())
            await svc.InitializeLeaseContainer(ct);
    }
}