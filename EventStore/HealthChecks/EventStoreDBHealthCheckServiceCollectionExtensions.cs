using EventStore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthChecks.EventStoreDB.Grpc;

public static class EventStoreDBHealthCheckServiceCollectionExtensions
{
    public static IHealthChecksBuilder AddEventStore(this IHealthChecksBuilder builder,
        Action<EventStoreDBOptions> configureOptions,
        string name = "EventStore",
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<EventStoreDBHealthCheck>(sp =>
        {
            var options = new EventStoreDBOptions();
            configureOptions(options);
            var settings = options.CreateSettings(sp);
            return new EventStoreDBHealthCheck(new EventStoreClient(settings));
        });
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<EventStoreDBHealthCheck>(),
            failureStatus,
            tags,
            timeout));
    }
}