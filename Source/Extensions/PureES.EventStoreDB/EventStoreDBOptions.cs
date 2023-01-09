using EventStore.Client;
using Microsoft.Extensions.Http.Logging;
using Microsoft.Extensions.Logging;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES.EventStoreDB;

public class EventStoreDBOptions
{
    /// <summary>
    /// Gets or sets the EventStore Nodes use
    /// </summary>
    public ISet<string> Nodes { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets the credentials used to authenticate to EventStoreDB in format format <c>username:password</c>
    /// </summary>
    public string? Credentials { get; set; }

    /// <summary>
    /// Indicates whether the EventStore node(s) are secured with TLS (default <see langword="true"/>)
    /// </summary>
    public bool UseTLS { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the EventStore TLS certificates should be verified (default <see langword="true"/>)
    /// </summary>
    /// <remarks>
    /// If node(s) are using self-signed certificates, set to <see langword="true"/>, otherwise <see langword="false"/>
    /// </remarks>
    public bool VerifyTLSCert { get; set; } = true;

    /// <summary>
    /// Gets or sets whether logging should be enabled (default <see langword="true"/>)
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    public void Validate()
    {
        if (Nodes == null! || Nodes.Count == 0)
            throw new Exception("EventStore URL(s) are required");
    }

    public EventStoreClientSettings CreateSettings(ILoggerFactory loggerFactory)
    {
        Validate();
        var credentials = Credentials != null ? $"{Credentials}@" : null;
        var url = string.Join(",", Nodes);
        var settings = EventStoreClientSettings.Create($"esdb://{credentials}{url}?tls={UseTLS}&tlsVerifyCert={VerifyTLSCert}");
        if (EnableLogging)
            settings.LoggerFactory = loggerFactory;
        return settings;
    }
}