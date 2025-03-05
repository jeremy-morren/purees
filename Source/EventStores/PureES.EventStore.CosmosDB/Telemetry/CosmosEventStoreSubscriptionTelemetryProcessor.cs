using JetBrains.Annotations;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace PureES.EventStore.CosmosDB.Telemetry;

/// <summary>
/// <see cref="CosmosEventStoreSubscriptionTelemetryInitializer"/>
/// </summary>
[UsedImplicitly]
internal class CosmosEventStoreSubscriptionTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;

    public CosmosEventStoreSubscriptionTelemetryProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        if (item.Context.Operation.SyntheticSource == CosmosEventStoreSubscriptionTelemetryInitializer.SyntheticSource)
            return;
        _next.Process(item);
    }
}