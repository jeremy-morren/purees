using PureES.EventBus;

namespace PureES.EventStore.InMemory.Subscription;

internal interface IInMemoryEventStoreSubscription : IEventStoreSubscription
{
    /// <summary>
    /// Notify that records have been committed
    /// </summary>
    public void AfterCommit(List<InMemoryEventRecord> records);
}