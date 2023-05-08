using System.Text.Json;
using Azure.Identity;
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

        if (!string.IsNullOrEmpty(_options.ConnectionString))
            Client = new CosmosClient(_options.ConnectionString, clientOptions);
        else if (_options.UseManagedIdentity)
            Client = new CosmosClient(_options.AccountEndpoint!, new DefaultAzureCredential(), clientOptions);
        else
            Client = new CosmosClient(_options.AccountEndpoint!, _options.AccountKey!, clientOptions);
    }

    public const string HttpClientName = "CosmosEventStore";

    private Task<Container>? _container;
    
    public Task<Container> GetEventStoreContainerAsync(CancellationToken ct)
    {
        _container ??= CreateContainerIfNotExists(ct);
        return _container;
    }

    private async Task<Container> CreateContainerIfNotExists(CancellationToken ct)
    {
        Database database = await Client.CreateDatabaseIfNotExistsAsync(_options.Database,
            _options.DatabaseThroughput,
            cancellationToken: ct);
        
        var container = database.DefineContainer(_options.Container, "/eventStreamId");

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
        return await container.CreateIfNotExistsAsync(_options.ContainerThroughput, ct);
    }
}