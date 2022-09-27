using EventStore.Client;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES.EventStoreDB.Subscriptions;


public class SubscriptionOptions
{
    public string SubscriptionId { get; set; } = "default";
    public SubscriptionFilterOptions FilterOptions { get; set; } =
        new(EventTypeFilter.ExcludeSystemEvents());
    public bool ResolveLinkTos { get; set; }

    public TimeSpan ResubscribeDelay { get; set; } = TimeSpan.FromSeconds(10);
}