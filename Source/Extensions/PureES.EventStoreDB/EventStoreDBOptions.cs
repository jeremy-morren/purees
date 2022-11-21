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
    ///     Whether the server certificate should be validated
    ///     i.e. disable self-signed certificate chains
    /// </summary>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>
    ///     Configure logging
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
            if (!ValidateCertificate)
                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            return handler;
        };
        return settings;
    }
}