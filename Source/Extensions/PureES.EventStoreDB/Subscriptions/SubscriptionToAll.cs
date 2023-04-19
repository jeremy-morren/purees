using EventStore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.EventBus;

namespace PureES.EventStoreDB.Subscriptions;

internal class SubscriptionToAll : SubscriptionService
{
    public SubscriptionToAll(
        EventStoreClient eventStoreClient,
        IEventBus eventBus,
        EventStoreDBSerializer serializer,
        ILoggerFactory loggerFactory,
        IOptionsFactory<SubscriptionOptions> optionsFactory)
        : base(eventStoreClient, eventBus, serializer, loggerFactory, optionsFactory)
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