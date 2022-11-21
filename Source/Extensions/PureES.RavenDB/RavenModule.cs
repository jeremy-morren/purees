using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.Json.Serialization.NewtonsoftJson;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
// ReSharper disable MemberCanBePrivate.Global

// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.RavenDB;

public static class RavenModule
{
    public static IServiceCollection AddRavenDB(this IServiceCollection services,
        Action<RavenDBOptions> configureOptions)
    {
        services.AddOptions<RavenDBOptions>()
            .Configure(o => configureOptions?.Invoke(o))
            .Validate(o =>
            {
                if (string.IsNullOrWhiteSpace(o.Database))
                    throw new InvalidOperationException("RavenDB database is required");
                // ReSharper disable once ConstantConditionalAccessQualifier
                if ((o.Urls?.Length ?? 0) == 0)
                    throw new InvalidOperationException("RavenDB url is required");
                return true;
            });

        return services
            .AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RavenDBOptions>>().Value;
                return new DocumentStore
                    {
                        Urls = options.Urls,
                        Database = options.Database,
                        Certificate = !string.IsNullOrEmpty(options.Certificate)
                            ? new X509Certificate2(options.Certificate)
                            : null,
                        Conventions =
                        {
                            FindCollectionName = t =>
                            {
                                var attr = t.GetCustomAttribute<RavenCollectionAttribute>();
                                return attr?.Name ?? DocumentConventions.DefaultGetCollectionName(t);
                            }
                        }
                    }
                    .SetConventions(options)
                    .Initialize()
                    .EnsureDatabaseCreated(options.Database);
            })
            .AddScoped<IAsyncDocumentSession>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RavenDBOptions>>().Value;
                return sp.GetRequiredService<IDocumentStore>()
                    .OpenAsyncSession(new SessionOptions
                    {
                        Database = options.Database,
                        NoTracking = true
                    });
            })
            .AddScoped<IDocumentSession>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RavenDBOptions>>().Value;
                return sp.GetRequiredService<IDocumentStore>()
                    .OpenSession(new SessionOptions
                    {
                        Database = options.Database,
                        NoTracking = true
                    });
            });
    }

    private static IDocumentStore EnsureDatabaseCreated(this IDocumentStore store, string? database)
    {
        if (string.IsNullOrWhiteSpace(database))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(database));
        try
        {
            store.Maintenance.ForDatabase(database).Send(new GetStatisticsOperation());
        }
        catch (DatabaseDoesNotExistException)
        {
            try
            {
                store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(database)));
            }
            catch (ConcurrencyException)
            {
                // The database was already created before calling CreateDatabaseOperation
            }
        }

        return store;
    }

    public static IDocumentStore SetConventions(this IDocumentStore store, RavenDBOptions options)
    {
        store.Conventions.FindIdentityProperty =
            m => "RavenId".Equals(m.Name, StringComparison.InvariantCultureIgnoreCase);

        //By default ravendb serializes private members as well
        DefaultRavenContractResolver.MembersSearchFlag = BindingFlags.Public | BindingFlags.Instance;
        store.Conventions.Serialization = new NewtonsoftJsonSerializationConventions
        {
            CustomizeJsonSerializer = s => options.ConfigureSerializer?.Invoke(s)
        };
        return store;
    }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RavenDBOptions
{
    public string[] Urls { get; set; } = null!;
    public string Database { get; set; } = null!;
    public string? Certificate { get; set; }

    public Action<JsonSerializer>? ConfigureSerializer { get; set; }
}