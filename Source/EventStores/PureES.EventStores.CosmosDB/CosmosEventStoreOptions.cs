// ReSharper disable InconsistentNaming

using System.Text.Json;
using JetBrains.Annotations;

namespace PureES.EventStores.CosmosDB;

[PublicAPI]
public class CosmosEventStoreOptions
{
    /// <summary>
    /// Gets or sets the CosmosDB connection string.
    /// </summary>
    /// <remarks>
    /// This must reference the API for NoSQL endpoint
    /// </remarks>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the cosmos account key or resource token to use to create the client.
    /// </summary>
    /// <remarks>
    /// This must reference the API for NoSQL endpoint. Will be ignored if <see cref="ConnectionString"/> is set
    /// </remarks>
    public string? AccountEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the cosmos account key or resource token to use to create the client.
    /// </summary>
    /// <remarks>
    /// Will be ignored if <see cref="ConnectionString"/> is set or <see cref="UseManagedIdentity"/> is <see langword="true"/>
    /// </remarks>
    public string? AccountKey { get; set; }

    /// <summary>
    /// Indicates whether to use Azure managed identity instead of instead of <see cref="AccountKey"/>.
    /// Default <see langword="false"/>
    /// </summary>
    public bool UseManagedIdentity { get; set; }

    /// <summary>
    /// Gets or sets a flag indicating whether the server TLS certificate should not be verified.
    /// Default <see langword="false" />
    /// </summary>
    public bool Insecure { get; set; }

    /// <summary>
    /// The Cosmos client options
    /// </summary>
    public CosmosClientOptions ClientOptions { get; } = new()
    {
        EnableContentResponseOnWrite = false
    };

    /// <summary>
    /// Gets or sets the Cosmos database
    /// </summary>
    public string Database { get; set; } = null!;

    /// <summary>
    /// Gets or sets the throughput to provision for <see cref="Database"/>,
    /// if database must be created
    /// </summary>
    public int? DatabaseThroughput { get; set; }

    /// <summary>
    /// The name of the container to use for events
    /// </summary>
    public string Container { get; set; } = "EventStore";
    
    /// <summary>
    /// The name of the lease container for subscriptions
    /// </summary>
    public string SubscriptionsLeaseContainerName { get; set; } = "EventStoreSubscriptions";

    /// <summary>
    /// The throughput to provision for the container
    /// if the container must be created
    /// </summary>
    public int? ContainerThroughput { get; set; }
    
    /// <summary>
    /// The throughput to provision for the subscriptions lease container
    /// if the container must be created
    /// </summary>
    public int? SubscriptionsLeaseContainerThroughput { get; set; }

    /// <summary>
    /// Gets or sets the JSON serializer options to use
    /// when deserializing events & metadata
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Gets or sets the type to deserialize metadata as
    /// </summary>
    public Type MetadataType { get; set; } = typeof(JsonElement?);

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            if (string.IsNullOrWhiteSpace(AccountEndpoint))
                throw new Exception("Either Cosmos connection string or AccountEndpoint/AccountKey must be provided");

            //Account endpoint was provided
            try
            {
                _ = new Uri(AccountEndpoint, UriKind.Absolute);
            }
            catch (Exception e)
            {
                throw new Exception("Invalid Cosmos EventStore account endpoint", e);
            }

            if (!UseManagedIdentity && string.IsNullOrWhiteSpace(AccountKey))
                throw new Exception($"Cosmos EventStore account key is required if {nameof(UseManagedIdentity)} is false");
        }
        
        if (string.IsNullOrWhiteSpace(Database))
            throw new Exception("Cosmos EventStore database is required");
        if (DatabaseThroughput is <= 0)
            throw new Exception("Cosmos EventStore database throughput must be greater than 0");
        
        if (string.IsNullOrWhiteSpace(Container))
            throw new Exception("Cosmos EventStore container is required");

        if (string.IsNullOrWhiteSpace(SubscriptionsLeaseContainerName))
            throw new Exception("Cosmos EventStore subscription lease container is required");

        if (ContainerThroughput is <= 0)
            throw new Exception("Cosmos EventStore container throughput must be greater than 0");

        if (MetadataType == null)
            throw new Exception("Cosmos EventStore Metadata type is required");
    }
}