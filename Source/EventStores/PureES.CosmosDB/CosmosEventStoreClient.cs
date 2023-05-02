using System.Text.Json;
using Microsoft.Extensions.Options;
using PureES.CosmosDB.Serialization;

namespace PureES.CosmosDB;

internal class CosmosEventStoreClient
{

    public readonly CosmosSystemTextJsonSerializer Serializer;
    
    private readonly CosmosEventStoreOptions _options;
    public readonly CosmosClient Client;

    public CosmosEventStoreClient(IOptions<CosmosEventStoreOptions> options, 
        IHttpClientFactory httpClientFactory,
        IServiceProvider services)
    {
        _options = options.Value;

        var clientOptions = _options.ClientOptions;
        
        if (!_options.VerifyTLSCert)
            clientOptions.ServerCertificateCustomValidationCallback = (_, _, _) => true;
        
        clientOptions.HttpClientFactory = options.Value.HttpClientFactory != null 
            ? () => options.Value.HttpClientFactory(services)
            : () => httpClientFactory.CreateClient(HttpClientName);
        
        clientOptions.SerializerOptions = null;
        
        //Note that this serializer is not the one used to deserialize events/metadata
        Serializer = new CosmosSystemTextJsonSerializer(new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false
        });

        clientOptions.Serializer = Serializer;

        Client = string.IsNullOrEmpty(_options.ConnectionString)
            ? new CosmosClient(_options.AccountEndpoint, _options.AccountKey, clientOptions)
            : new CosmosClient(_options.ConnectionString, clientOptions);
    }

    public const string HttpClientName = "CosmosEventStore";

    private Database? _database;
    private Container? _container;
    
    public async ValueTask<Container> GetEventStoreContainerAsync(CancellationToken ct)
    {
        if (_container != null) return _container;
        
        _database ??= await Client.CreateDatabaseIfNotExistsAsync(_options.Database,
            _options.DatabaseThroughput,
            cancellationToken: ct);
        
        var container = _database.DefineContainer(_options.Container, "/eventStreamId");

        var builder = container.WithIndexingPolicy();

        builder = builder.WithIndexingMode(IndexingMode.Consistent)
            .WithAutomaticIndexing(false)
            .WithIncludedPaths()
            .Attach();

        //Note: For reading, we need OrderBy as follows:
        //created, eventStreamId, eventStreamPosition

        //We also need to be able to filter by /eventType

        //Read Single stream
        builder = builder.WithCompositeIndex()
            .Path("/eventStreamId", CompositePathSortOrder.Ascending)
            .Path("/eventStreamPosition", CompositePathSortOrder.Ascending)
            .Attach();

        //Read All
        builder = builder.WithCompositeIndex()
            .Path("/_ts", CompositePathSortOrder.Ascending)
            .Path("/created", CompositePathSortOrder.Ascending)
            .Path("/eventStreamId", CompositePathSortOrder.Ascending)
            .Path("/eventStreamPosition", CompositePathSortOrder.Ascending)
            .Attach();

        //Read multiple streams
        builder = builder.WithCompositeIndex()
            .Path("/eventStreamId", CompositePathSortOrder.Ascending)
            .Path("/_ts", CompositePathSortOrder.Ascending)
            .Path("/created", CompositePathSortOrder.Ascending)
            .Path("/eventStreamPosition", CompositePathSortOrder.Ascending)
            .Attach();

        //Read by event type
        builder = builder.WithCompositeIndex()
            .Path("/eventType", CompositePathSortOrder.Ascending)
            .Path("/_ts", CompositePathSortOrder.Ascending)
            .Path("/created", CompositePathSortOrder.Ascending)
            .Path("/eventStreamId", CompositePathSortOrder.Ascending)
            .Path("/eventStreamPosition", CompositePathSortOrder.Ascending)
            .Attach();

        container = builder.Attach();
        _container = await container.CreateIfNotExistsAsync(_options.ContainerThroughput, ct);
        return _container;
    }
}