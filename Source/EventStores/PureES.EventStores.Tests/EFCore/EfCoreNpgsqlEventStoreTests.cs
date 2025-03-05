using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PureES.EventStore.EFCore;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable UseAwaitUsing

namespace PureES.EventStores.Tests.EFCore;

public class EfCoreNpgsqlEventStoreTests : EfCoreEventStoreTestsBase
{
    private readonly ITestOutputHelper _output;

    public EfCoreNpgsqlEventStoreTests(ITestOutputHelper output)
    {
        _output = output;
        EnsureDatabaseExists();
    }

    [Fact]
    public async Task CreateScriptShouldBeIdempotent()
    {
        var schema = nameof(CreateScriptShouldBeIdempotent).ToLowerInvariant();
        
        await Execute($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        
        var services = new ServiceCollection();

        services.AddDbContext<EmptyDbContext>(b => b.UseNpgsql($"{ConnString};Database={DbName}"));

        services.AddEfCoreEventStore<EmptyDbContext>(o => o.Schema = schema);
        
        services.AddPureES().AddBasicEventTypeMap();
        
        var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<IEfCoreEventStore>();
        var script = store.GenerateIdempotentCreateScript();
        script.ShouldContain(schema);
        _output.WriteLine(script);
        await Execute(script);
        await Execute(script); //Should not throw
        
        //Read events should succeed
        (await store.ReadAll().ToListAsync()).ShouldBeEmpty();
    }
    
    private const string ConnString = "Host=localhost;Username=postgres;Password=postgres";
    private const string DbName = "purees_efcore_tests";
    
    protected override async Task<EventStoreTestHarness> CreateStore(string testName, Action<IServiceCollection> configureServices, CancellationToken ct)
    {
        var schema = new [] { '-', '.', '+'}.Aggregate(testName, (current, c) => current.Replace(c, '_')).ToLowerInvariant();
        
        var services = new ServiceCollection();

        services.AddDbContext<EmptyDbContext>(b => b.UseNpgsql($"{ConnString};Database={DbName}"));

        services.AddEfCoreEventStore<EmptyDbContext>()
            .Configure(o => o.Schema = schema)
            .AddSubscriptionToAll();

        services.AddPureES().AddBasicEventTypeMap();
        
        configureServices(services);

        var sp = services.BuildServiceProvider();

        var store = sp.GetRequiredService<IEventStore>();
        var harness = new NpgsqlEventStoreTestHarness(sp, schema);
        await harness.DropSchema(); //Drop the schema to ensure a clean slate

        using (var context = sp.GetRequiredService<EventStoreDbContext<EmptyDbContext>>())
        {
            var script = context.Database.GenerateCreateScript();
            await Execute(script);
        }

        return new EventStoreTestHarness(harness, store);
    }
    
    private class NpgsqlEventStoreTestHarness : IAsyncDisposable, IServiceProvider
    {
        private readonly ServiceProvider _services;
        private readonly string _schema;

        public NpgsqlEventStoreTestHarness(ServiceProvider services, string schema)
        {
            _services = services;
            _schema = schema;
        }
        
        public async ValueTask DisposeAsync()
        {
            await _services.DisposeAsync();
            await DropSchema();
        }

        public Task DropSchema() => Execute($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
        public object? GetService(Type serviceType) => _services.GetService(serviceType);
    }
    
    private static void EnsureDatabaseExists()
    {
        //Create database if it doesn't exist
        try
        {
            ExecuteMaster($"CREATE DATABASE \"{DbName}\"");
        }
        catch (NpgsqlException e) when (e.SqlState == "42P04")
        {
            // Database already exists
        }
    }
    
    private static void ExecuteMaster(string sql)
    {
        using var conn = new NpgsqlConnection(ConnString);
        if (conn.State != ConnectionState.Open)
            conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
    
    private static async Task Execute(string sql)
    {
        using var conn = new NpgsqlConnection($"{ConnString};Database={DbName}");
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}