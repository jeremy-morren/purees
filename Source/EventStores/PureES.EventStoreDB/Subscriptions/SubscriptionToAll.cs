using EventStore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PureES.EventStoreDB.Subscriptions;

internal class SubscriptionToAll : SubscriptionService
{
    public SubscriptionToAll(EventStoreClient eventStoreClient, 
        EventStoreDBSerializer serializer, 
        IOptionsFactory<SubscriptionOptions> optionsFactory, 
        IServiceProvider services, 
        ILoggerFactory? loggerFactory = null) 
        : base(eventStoreClient, serializer, optionsFactory, services, loggerFactory)
    {
        
    }
    
    protected override Task<StreamSubscription> Subscribe(EventStoreClient client,
        FromAll start,
        Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
        bool resolveLinkTos,
        Action<StreamSubscription, SubscriptionDroppedReason, Exception?>? subscriptionDropped,
        SubscriptionFilterOptions? filterOptions,
        CancellationToken cancellationToken) =>
        client.SubscribeToAllAsync(start, eventAppeared, resolveLinkTos, subscriptionDropped, filterOptions, null,
            cancellationToken);

}