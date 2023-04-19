using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PureES.Core.EventStore;
using PureES.CosmosDB;
using PureES.EventBus;

// ReSharper disable StringLiteralTypo
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.Extensions.Tests.EventStore.Cosmos;

public sealed class CosmosTestFixture : IDisposable, IAsyncDisposable, IServiceProvider
{
    private readonly ServiceProvider _services;
    
    public readonly string Id;

    public CosmosTestFixture() : this(_ => {}) { }

    internal CosmosTestFixture(Action<IServiceCollection> configureServices)
    {
        Id = Guid.NewGuid().ToString();
        var services = new ServiceCollection()
            .AddSingleton<IEventTypeMap>(TestSerializer.EventTypeMap)
            .AddCosmosEventStore(options =>
            {
                options.Database = Id;
                options.DatabaseThroughput = 10_000;

                options.Container = Id;
                options.ContainerThroughput = 10_000;

                options.VerifyTLSCert = false;
                options.AccountEndpoint = "https://localhost:8081/";
                options.AccountKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            });

        configureServices(services);
        _services = services.BuildServiceProvider();
    }

    public override string ToString() => Id;
    
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

    public object? GetService(Type serviceType) => _services.GetService(serviceType);
    
    public void Dispose()
    {
        try
        {
            DeleteDatabase().GetAwaiter().GetResult();
        }
        finally
        {
            _services.Dispose();
        }
    }

    private async Task DeleteDatabase()
    {
        var client = _services.GetRequiredService<CosmosEventStoreClient>().Client;
        //Note: if it failed, we don't care - i.e. it was already deleted
        await client.GetDatabase(Id).DeleteStreamAsync();
        await client.GetDatabase("Audits").DeleteStreamAsync();
    }
}