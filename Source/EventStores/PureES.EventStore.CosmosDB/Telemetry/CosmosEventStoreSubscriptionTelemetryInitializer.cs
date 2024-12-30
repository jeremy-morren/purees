using JetBrains.Annotations;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace PureES.EventStore.CosmosDB.Telemetry;

/// <summary>
/// Filters out verbose telemetry from CosmosEventStoreSubscription
/// </summary>
/// <remarks>
/// <para>Subscriptions are implemented using Polling, which generates a lot of telemetry</para>
/// <para>It is also not telemetry that we are interested in (because it doesn't impact performance)</para>
/// </remarks>
[UsedImplicitly]
internal class CosmosEventStoreSubscriptionTelemetryInitializer : ITelemetryInitializer
{
    private readonly string _eventStore;
    private readonly string _lease;
    
    public CosmosEventStoreSubscriptionTelemetryInitializer(IOptions<CosmosEventStoreOptions> options)
    {
        var o = options.Value;
        _eventStore = o.Container;
        _lease = o.SubscriptionsLeaseContainerName;
    }
    
    public const string SyntheticSource = "CosmosEventStoreSubscription";
    
    public void Initialize(ITelemetry telemetry)
    {
        if (!ShouldFilter(telemetry))
            return;
        
        /*
         * On startup, we get telemetry for:
         */
        
        // We don't want to track subscriptions in metrics
        if (telemetry is ISupportAdvancedSampling advancedSampling)
            advancedSampling.ProactiveSamplingDecision = SamplingDecision.SampledOut;
        
        // For the case that we cannot filter out the telemetry, we mark it as synthetic
        if (string.IsNullOrWhiteSpace(telemetry.Context.Operation.SyntheticSource))
            telemetry.Context.Operation.SyntheticSource = SyntheticSource;
    }

    private bool ShouldFilter(ITelemetry telemetry)
    {
        if (telemetry is not DependencyTelemetry { Type: "Microsoft.DocumentDB"} d)
            return false;

        if (d.Success.HasValue && !d.Success.Value)
            return false;

        var operation = d.Properties["db.operation"];
        var container = d.Properties.TryGetValue("db.cosmosdb.container", out var c) ? c : null;

        return operation switch
        {
            "Change Feed Processor Read Next Async" => 
                d.ResultCode == "304" && container == _eventStore,
            
            "ReadItemStreamAsync" or "ReplaceItemStreamAsync" or "FeedIterator Read Next Async" =>
                d.ResultCode == "200" && container == _lease,
            
            _ => false
        };
    }
}