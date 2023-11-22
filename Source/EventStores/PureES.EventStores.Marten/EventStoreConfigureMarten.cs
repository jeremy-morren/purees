using System.Linq.Expressions;
using Marten;
using Marten.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PureES.EventStores.Marten.Subscriptions;

namespace PureES.EventStores.Marten;

internal class EventStoreConfigureMarten : IConfigureMarten
{
    private readonly IOptions<MartenEventStoreOptions> _options;
    private readonly List<IMartenEventStoreSubscription> _subscriptions;

    public EventStoreConfigureMarten(IOptions<MartenEventStoreOptions> options,
        IEnumerable<IHostedService> hostedServices)
    {
        _options = options;
        _subscriptions = hostedServices.OfType<IMartenEventStoreSubscription>().ToList();
    }

    public void Configure(IServiceProvider services, StoreOptions options)
    {
        options.Schema.For<MartenEvent>()
            .Identity(x => x.Id)
            .DatabaseSchemaName(_options.Value.DatabaseSchema)
            .UniqueIndex(UniqueIndexType.Computed, x => x.StreamId, x => x.StreamPosition)
            .Index(new Expression<Func<MartenEvent, object>>[] { i => i.EventType, i => i.Timestamp })
            .Metadata(c =>
            {
                c.LastModified.MapTo(x => x.Timestamp);
                c.DotNetType.Enabled = false;
                c.Version.Enabled = false;
            })
            .IndexLastModified()
            .Index(i => i.EventType);

        foreach (var s in _subscriptions)
            options.Listeners.Add(s.Listener);
    }
}