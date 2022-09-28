using System.Diagnostics.CodeAnalysis;
using EventStore.Client;

namespace AspNetCore.HealthChecks.EventStoreDB;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class EventStoreDBOptions
{
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Whether the server certificate should be validated
    /// i.e. disable self-signed certificate chains
    /// </summary>
    public bool ValidateCertificate { get; set; } = true;

    public EventStoreClientSettings CreateSettings()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("Connection string is required");
        var settings = EventStoreClientSettings.Create(ConnectionString);
        settings.CreateHttpMessageHandler = () =>
        {
            //TODO: Add Polly
            var handler = new SocketsHttpHandler();
            if (!ValidateCertificate)
                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            return handler;
        };
        return settings;
    }
}