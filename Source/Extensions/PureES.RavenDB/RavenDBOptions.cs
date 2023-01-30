using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
// ReSharper disable MemberCanBePrivate.Global

namespace PureES.RavenDB;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RavenDBOptions
{
    public HashSet<string> Urls { get; set; } = null!;
    public string Database { get; set; } = null!;
    public string? Certificate { get; set; }

    public Action<JsonSerializer>? ConfigureSerializer { get; set; }

    public void Validate()
    {
        if (Urls == null! || Urls.Count == 0)
            throw new Exception("RavenDB url(s) are required");
        foreach (var u in Urls)
        {
            try
            {
                _ = new Uri(u, UriKind.Absolute);
            }
            catch (Exception e)
            {
                throw new Exception($"RavenDB URL '{u}' is invalid", e);
            }
        }
        if (string.IsNullOrWhiteSpace(Database))
            throw new Exception("RavenDB database is required");
        if (Certificate != null)
            _ = GetCertificate();
    }

    [Pure]
    public X509Certificate2? GetCertificate()
    {
        if (string.IsNullOrWhiteSpace(Certificate)) return null;
        try
        {
            if (File.Exists(Certificate))
                return new X509Certificate2(Certificate);
            var bytes = Convert.FromBase64String(Certificate);
            return new X509Certificate2(bytes);
        }
        catch (Exception e)
        {
            throw new Exception("Certificate must be either a valid .pfx filepath or a base64-encoded certificate", e);
        }
    }
}