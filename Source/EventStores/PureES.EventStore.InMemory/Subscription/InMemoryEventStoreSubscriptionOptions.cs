using PureES.EventBus;

namespace PureES.EventStore.InMemory.Subscription;

public class InMemoryEventStoreSubscriptionOptions : EventBusOptions
{
    public EventBusOptions EventBusOptions { get; } = new();
}