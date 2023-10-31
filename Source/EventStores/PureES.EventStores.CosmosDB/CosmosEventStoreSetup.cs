using Microsoft.Extensions.DependencyInjection;

namespace PureES.EventStores.CosmosDB;

public static class CosmosEventStoreSetup
{
    public static Task InitializeEventStore(IServiceProvider services, CancellationToken ct)
    {
        var svc = services.GetRequiredService<CosmosEventStoreClient>();
        
        return svc.CreateContainerIfNotExists(ct);
    }
}