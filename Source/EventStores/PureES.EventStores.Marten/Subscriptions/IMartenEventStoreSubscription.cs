using Marten;
using PureES.EventBus;

namespace PureES.EventStores.Marten.Subscriptions;

internal interface IMartenEventStoreSubscription : IEventStoreSubscription
{
    public IDocumentSessionListener Listener { get; }
}