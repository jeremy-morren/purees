using PureES.Core;
using PureES.EventBus;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace PureES.RavenDB;

internal class SyncRavenEventHandler<TEvent, TMetadata> : IEventHandler<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    private readonly Action<IServiceProvider, IAsyncDocumentSession, EventEnvelope<TEvent, TMetadata>> _delegate;
    private readonly RavenDBOptions _options;
    private readonly IServiceProvider _services;
    private readonly IDocumentStore _store;

    public SyncRavenEventHandler(IDocumentStore store,
        RavenDBOptions options,
        Action<IServiceProvider, IAsyncDocumentSession, EventEnvelope<TEvent, TMetadata>> @delegate,
        IServiceProvider services)
    {
        _store = store;
        _options = options;
        _delegate = @delegate;
        _services = services;
    }

    public async Task Handle(EventEnvelope<TEvent, TMetadata> @event, CancellationToken ct)
    {
        using var session = _store.OpenAsyncSession(_options.Database);
        _delegate(_services, session, @event);
        await session.SaveChangesAsync(ct);
    }
}