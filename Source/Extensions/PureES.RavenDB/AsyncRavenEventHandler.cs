using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.EventBus;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace PureES.RavenDB;

internal class AsyncRavenEventHandler<TEvent, TMetadata> : IEventHandler<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    private readonly IOptions<RavenDBOptions> _options;
    private readonly IServiceProvider _services;
    private readonly Func<IServiceProvider, IAsyncDocumentSession, EventEnvelope<TEvent, TMetadata>, CancellationToken, Task> _delegate;
    private readonly IDocumentStore _store;

    public AsyncRavenEventHandler(IDocumentStore store,
        IOptions<RavenDBOptions> options,
        Func<IServiceProvider, IAsyncDocumentSession, EventEnvelope<TEvent, TMetadata>, CancellationToken, Task> @delegate,
        IServiceProvider services)
    {
        _store = store;
        _options = options;
        _delegate = @delegate;
        _services = services;
    }

    public async Task Handle(EventEnvelope<TEvent, TMetadata> @event, CancellationToken ct)
    {
        using var session = _store.OpenAsyncSession(_options.Value.Database);
        await _delegate(_services, session, @event, ct);
        await session.SaveChangesAsync(ct);
    }
}