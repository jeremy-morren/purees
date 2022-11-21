using System.Diagnostics.CodeAnalysis;
using EventStore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthChecks.EventStoreDB.Grpc;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class EventStoreDBOptions
{
    public string ConnectionString { get; set; } = null!;
    
    /// <summary>
    /// Configure logging
    /// </summary>
    public bool EnableLogging { get; set; } = false;

    public EventStoreClientSettings CreateSettings(IServiceProvider services)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("Connection string is required");
        var settings = EventStoreClientSettings.Create(ConnectionString);
        settings.CreateHttpMessageHandler = () =>
        {
            var handler = new SocketsHttpHandler();
            if (EnableLogging)
                settings.LoggerFactory = services.GetRequiredService<ILoggerFactory>();
            return handler;
        };
        return settings;
    }
}