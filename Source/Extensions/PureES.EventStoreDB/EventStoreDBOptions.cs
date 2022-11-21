using System.Diagnostics.CodeAnalysis;
using EventStore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PureES.EventStoreDB;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class EventStoreDBOptions
{
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether the eventstore TLS certificate should
    /// be validated. Defaults to <c>true</c>
    /// </summary>
    /// <remarks>
    /// Set to <c>true</c> to allow self-signed certificates
    /// </remarks>
    public bool ValidateServerCertificate { get; set; } = true;

    /// <summary>Gets or sets whether HTTP logging should be enabled. Defaults to <c>false</c></summary>
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
            if (!ValidateServerCertificate)
                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            return handler;
        };
        return settings;
    }
}