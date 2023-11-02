using System.Net;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Options;
using PureES.EventStores.CosmosDB.Serialization;

namespace PureES.EventStores.CosmosDB;

internal class CosmosEventStoreClient
{
    public readonly CosmosSystemTextJsonSerializer Serializer;
    
    private readonly CosmosEventStoreOptions _options;
    public readonly CosmosClient Client;

    public CosmosEventStoreClient(IOptions<CosmosEventStoreOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;

        var clientOptions = _options.ClientOptions;
        
        if (_options.Insecure)
            clientOptions.ServerCertificateCustomValidationCallback = (_, _, _) => true;
        
        clientOptions.HttpClientFactory = () => httpClientFactory.CreateClient(HttpClientName);
        
        clientOptions.SerializerOptions = null;
        
        //Note that this serializer is not the one used to deserialize events/metadata
        Serializer = new CosmosSystemTextJsonSerializer(new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false
        });

        clientOptions.Serializer = Serializer;

        Client = !string.IsNullOrEmpty(_options.ConnectionString) 
            ? new CosmosClient(_options.ConnectionString, clientOptions) 
            : _options.UseManagedIdentity
                ? new CosmosClient(_options.AccountEndpoint!, new DefaultAzureCredential(), clientOptions)
                : new CosmosClient(_options.AccountEndpoint!, _options.AccountKey!, clientOptions);
    }

    public const string HttpClientName = "CosmosEventStore";

    public Container GetContainer() => Client.GetDatabase(_options.Database).GetContainer(_options.Container);
    
    public async Task<Database> CreateDatabaseIfNotExists(CancellationToken ct)
    {
        return await Client.CreateDatabaseIfNotExistsAsync(_options.Database,
            _options.DatabaseThroughput,
            cancellationToken: ct);
    }
    
    public async Task<Container> CreateContainerIfNotExists(CancellationToken ct)
    {
        var database = await CreateDatabaseIfNotExists(ct);

        const string partitionKeyPath = "/eventStreamId";

        var containerDef = DefineContainer(database.DefineContainer(_options.Container, partitionKeyPath)).Build();

        var container = database.GetContainer(_options.Container);
            
        var response = await container.ReadContainerStreamAsync(cancellationToken: ct);

        if (response.IsSuccessStatusCode)
            return await container.ReplaceContainerAsync(containerDef, cancellationToken: ct);

        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode(); //Throw if not 404

        return await database.CreateContainerAsync(containerDef, _options.ContainerThroughput, cancellationToken: ct);
    }

    private static ContainerBuilder DefineContainer(ContainerBuilder container)
    {
        //Exclude event and metadata from indexing, since we never filter there

        var builder = container.WithIndexingPolicy()
            .WithIndexingMode(IndexingMode.Consistent)
            .WithExcludedPaths()
                .Path("/event/*")
                .Path("/metadata/*")
            .Attach()
            .WithIncludedPaths()
                .Path("/*")
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

        return builder.Attach();
    }
}