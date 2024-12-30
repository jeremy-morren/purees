using Marten;
using PureES.EventBus;

namespace PureES.EventStore.Marten.Subscriptions;

internal interface IMartenEventStoreSubscription : IEventStoreSubscription
{
    public IDocumentSessionListener Listener { get; }
}