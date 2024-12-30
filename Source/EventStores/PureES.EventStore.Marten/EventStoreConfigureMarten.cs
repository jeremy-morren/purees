using Marten;
using Marten.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PureES.EventStore.Marten.CustomMethods;
using PureES.EventStore.Marten.Subscriptions;
using Weasel.Postgresql.Tables;

namespace PureES.EventStore.Marten;

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

    public void Configure(IServiceProvider _, StoreOptions options)
    {
        options.Linq.MethodCallParsers.Add(new IntersectsMethodCallParser());
        
        options.Schema.For<MartenEvent>()
            .Identity(x => x.Id)
            .DatabaseSchemaName(_options.Value.DatabaseSchema)
            .UniqueIndex(UniqueIndexType.Computed, x => x.StreamId, x => x.StreamPosition)
            .Metadata(c =>
            {
                c.LastModified.MapTo(x => x.Timestamp);
                c.DotNetType.Enabled = false;
                c.Version.Enabled = false;
            })
            
            //Index event types with gin for intersection queries
            .Index(i => i.EventTypes, i => i.Method = IndexMethod.gin)
            
            .IndexLastModified();
        
        foreach (var s in _subscriptions)
            options.Listeners.Add(s.Listener);
    }
}