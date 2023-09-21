using Microsoft.Extensions.DependencyInjection;
using PureES.Core.EventStore;
using PureES.CosmosDB;

// ReSharper disable StringLiteralTypo
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.EventStores.Tests.Cosmos;

internal sealed class CosmosTestHarness : IAsyncDisposable, IServiceProvider
{
    private readonly string _name;
    private readonly ServiceProvider _services;

    private CosmosTestHarness(string name, Action<IServiceCollection> configureServices)
    {
        _name = name;
        var services = new ServiceCollection()
            .AddSingleton<IEventTypeMap>(TestSerializer.EventTypeMap)
            .AddCosmosEventStore(options =>
            {
                options.Database = name;
                options.DatabaseThroughput = 400;

                options.Container = name;
                options.ContainerThroughput = 400;

                options.VerifyTLSCert = false;
                options.AccountEndpoint = "https://localhost:8081/";
                options.AccountKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            });
            
        configureServices(services);
        
        _services = services.BuildServiceProvider();
    }

    public object? GetService(Type serviceType) => _services.GetService(serviceType);

    public override string ToString() => _name;

    public static Task<CosmosTestHarness> Create(string name, CancellationToken ct) => Create(name, _ => { }, ct);
    
    public static async Task<CosmosTestHarness> Create(string name,
        Action<IServiceCollection> configureServices,
        CancellationToken ct)
    {
        var fixture = new CosmosTestHarness(name, configureServices);
        //Delete database
        await fixture.DeleteDatabase(); 
        //await fixture.Client.GetEventStoreContainerAsync(ct);
        return fixture;
    }
    
    public async ValueTask DisposeAsync()
    {
        try
        {
            await DeleteDatabase();
        }
        finally
        {
            await _services.DisposeAsync();
        }
    }

    private async Task DeleteDatabase()
    {
        var client = _services.GetRequiredService<CosmosEventStoreClient>().Client;
        //Note: if it failed, we don't care - i.e. it was already deleted
        await client.GetDatabase(_name).DeleteStreamAsync();
    }
}