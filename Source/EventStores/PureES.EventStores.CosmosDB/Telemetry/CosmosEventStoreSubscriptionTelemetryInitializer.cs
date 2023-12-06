using JetBrains.Annotations;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace PureES.EventStores.CosmosDB.Telemetry;

/// <summary>
/// Filters out verbose telemetry from CosmosEventStoreSubscription
/// </summary>
/// <remarks>
/// <para>Subscriptions are implemented using Polling, which generates a lot of telemetry</para>
/// <para>This processor filters out dependencies with status code 304 (i.e. not modified)</para>
/// </remarks>
[UsedImplicitly]
internal class CosmosEventStoreSubscriptionTelemetryInitializer : ITelemetryInitializer
{
    public const string SyntheticSource = "CosmosEventStoreSubscription";
    
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is not DependencyTelemetry d)
            return;
        if (d.Type != "Microsoft.DocumentDB" 
            || d.ResultCode != "304" 
            || !d.Name.EndsWith("Change Feed Processor Read Next Async", StringComparison.Ordinal))
            return;
        
        // We don't want to track subscriptions in metrics
        if (telemetry is ISupportAdvancedSampling advancedSampling)
            advancedSampling.ProactiveSamplingDecision = SamplingDecision.SampledOut;
        
        // For the case that we cannot filter out the telemetry, we mark it as synthetic
        if (string.IsNullOrWhiteSpace(telemetry.Context.Operation.SyntheticSource))
            telemetry.Context.Operation.SyntheticSource = SyntheticSource;
    }
}