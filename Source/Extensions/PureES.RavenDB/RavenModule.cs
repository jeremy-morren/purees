using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
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
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));
        
        services.AddOptions<RavenDBOptions>()
            .Configure(configureOptions.Invoke)
            .Validate(o =>
            {
                o.Validate();
                return true;
            });

        return services
            .Configure(configureOptions)
            .AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RavenDBOptions>>().Value;
                return new DocumentStore
                    {
                        Urls = options.Urls.ToArray(),
                        Database = options.Database,
                        Certificate = options.GetCertificate(),
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