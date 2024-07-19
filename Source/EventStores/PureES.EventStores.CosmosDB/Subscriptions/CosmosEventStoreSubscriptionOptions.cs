using JetBrains.Annotations;

namespace PureES.EventStores.CosmosDB.Subscriptions;

[PublicAPI]
public class CosmosEventStoreSubscriptionOptions
{
    /// <summary>
    /// Gets or sets the poll interval (in seconds) of the change feed processor
    /// </summary>
    /// <remarks>Defaults to 1 second</remarks>
    public double PollIntervalMilliseconds
    {
        get => PollInterval.TotalMilliseconds;
        set => PollInterval = TimeSpan.FromMilliseconds(value);
    }

    /// <summary>
    /// Gets or sets the poll interval of the change feed processor
    /// </summary>
    /// <remarks>Defaults to 1 second</remarks>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The name of the CosmosDB lease container to use for the change feed processor. Defaults to subscription name
    /// </summary>
    public string? LeaseContainerName { get; set; }

    /// <summary>
    /// The throughput to provision for the lease container,
    /// if it is necessary to create the container
    /// </summary>
    public int? LeaseContainerThroughput { get; set; }

    /// <summary>
    /// Gets or sets the name to use as change feed processor name. Defaults to subscription name
    /// </summary>
    public string? ChangeFeedProcessorName { get; set; }

    /// <summary>
    /// gets or sets the name to use as change feed instance name. Defaults to <see cref="Environment.MachineName"/>
    /// </summary>
    public string InstanceName { get; set; } = Environment.MachineName;

    /// <summary>
    /// Gets or sets the client elapsed threshold, above which a warning will be logged
    /// </summary>
    public TimeSpan ClientElapsedWarningThreshold { get; set; } = TimeSpan.FromSeconds(5);

    public void Validate()
    {
        if (PollInterval.Ticks <= 0)
            throw new Exception("Poll interval must be greater than 0");
        
        if (LeaseContainerName == string.Empty)
            throw new Exception($"{nameof(LeaseContainerName)} cannot be empty string");
        
        if (ChangeFeedProcessorName == string.Empty)
            throw new Exception($"{nameof(ChangeFeedProcessorName)} cannot be empty string");

        if (LeaseContainerThroughput is <= 0)
            throw new Exception($"{nameof(LeaseContainerThroughput)} must be greater than 0");

        if (string.IsNullOrWhiteSpace(InstanceName))
            throw new Exception("Instance name is required");
    }
}