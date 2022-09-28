using EventStore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetCore.HealthChecks.EventStoreDB;

public static class EventStoreDBHealthCheckServiceCollectionExtensions
{
    public static IHealthChecksBuilder AddEventStore(this IHealthChecksBuilder builder,
        Action<EventStoreDBOptions> configureOptions,
        string name = "EventStore",
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<EventStoreDBHealthCheck>(_ =>
        {
            var options = new EventStoreDBOptions();
            configureOptions(options);
            return new EventStoreDBHealthCheck(new EventStoreClient(options.CreateSettings()));
        });
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<EventStoreDBHealthCheck>(),
            failureStatus,
            tags,
            timeout));
    }
}