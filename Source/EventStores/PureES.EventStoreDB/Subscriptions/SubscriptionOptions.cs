using EventStore.Client;
using PureES.EventBus;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES.EventStoreDB.Subscriptions;

public class SubscriptionOptions
{
    public string SubscriptionId { get; set; } = "default";

    public SubscriptionFilterOptions FilterOptions { get; set; } =
        new(EventTypeFilter.ExcludeSystemEvents());

    /// <summary>
    ///     The length of time to wait before resubscribing
    /// </summary>
    public TimeSpan ResubscribeDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Indicates whether the current progress should be checkpointed to <c>EventStoreDB</c>
    /// </summary>
    public bool CheckpointToEventStoreDB { get; set; } = true;

    /// <summary>
    /// The event bus options
    /// </summary>
    public EventBusOptions EventBusOptions { get; } = new();
}