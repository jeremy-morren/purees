using PureES.EventBus;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore.Subscriptions;

/// <summary>
/// Interface for an EF Core event store subscription
/// </summary>
internal interface IEfCoreEventStoreSubscription : IEventStoreSubscription
{
    /// <summary>
    /// Invoked when events are written to the event store
    /// </summary>
    void OnEventsWritten(IEnumerable<EventStoreEvent> events);
}