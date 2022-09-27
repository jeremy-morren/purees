using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.EventBus;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace PureES.RavenDB;

public static class ProjectionServiceExtensions
{
    public static IServiceCollection On<TEvent, TMetadata>(this IServiceCollection services,
        Func<IServiceProvider, IAsyncDocumentSession, EventEnvelope<TEvent, TMetadata>, CancellationToken, Task> @delegate)
        where TEvent : notnull
        where TMetadata : notnull
        => services.AddEventHandler(sp => new AsyncRavenEventHandler<TEvent, TMetadata>(
            sp.GetRequiredService<IDocumentStore>(),
            sp.GetRequiredService<RavenDBOptions>(),
            @delegate,
            sp));

    public static IServiceCollection On<TEvent, TMetadata>(this IServiceCollection services,
        Action<IServiceProvider, IAsyncDocumentSession, EventEnvelope<TEvent, TMetadata>> @delegate)
        where TEvent : notnull
        where TMetadata : notnull =>
        services.AddEventHandler(sp => new SyncRavenEventHandler<TEvent, TMetadata>(
            sp.GetRequiredService<IDocumentStore>(),
            sp.GetRequiredService<RavenDBOptions>(),
            @delegate,
            sp));

    public static IServiceCollection On<TEvent, TMetadata>(this IServiceCollection services,
        Func<IAsyncDocumentSession, EventEnvelope<TEvent, TMetadata>, CancellationToken, Task> @delegate)
        where TEvent : notnull
        where TMetadata : notnull =>
        services.On<TEvent, TMetadata>((_, session, env, ct) => @delegate(session, env, ct));

    public static IServiceCollection On<TEvent, TMetadata>(this IServiceCollection services,
        Action<IAsyncDocumentSession, EventEnvelope<TEvent, TMetadata>> @delegate)
        where TEvent : notnull
        where TMetadata : notnull =>
        services.On<TEvent, TMetadata>((_, session, env) => @delegate(session, env));
}