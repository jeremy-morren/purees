using System.Threading.Tasks.Dataflow;
using EventStore.Client;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.EventBus;
using PureES.EventStoreDB.Serialization;

namespace PureES.EventStoreDB.Subscriptions;

internal class SubscriptionToAll : SubscriptionService
{
    public SubscriptionToAll(
        EventStoreClient eventStoreClient,
        IEventBus eventBus,
        IEventStoreDBSerializer serializer,
        ILoggerFactory loggerFactory,
        IOptionsFactory<SubscriptionOptions> optionsFactory)
        : base(eventStoreClient, eventBus, serializer, loggerFactory, optionsFactory) {}

    protected override Task<StreamSubscription> Subscribe(EventStoreClient client,
        FromAll start,
        Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
        bool resolveLinkTos,
        Action<StreamSubscription, SubscriptionDroppedReason, Exception?>? subscriptionDropped,
        SubscriptionFilterOptions? filterOptions,
        CancellationToken cancellationToken) =>
        client.SubscribeToAllAsync(start, eventAppeared, resolveLinkTos, subscriptionDropped, filterOptions, null, cancellationToken);
}
