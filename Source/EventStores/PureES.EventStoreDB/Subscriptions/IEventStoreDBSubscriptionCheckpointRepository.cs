namespace PureES.EventStoreDB.Subscriptions;

/// <summary>
/// A repository which stores a checkpoint for a subscription in <c>EventStoreDB</c>
/// </summary>
public interface IEventStoreDBSubscriptionCheckpointRepository
{
    ValueTask<ulong?> Load(string subscriptionId, CancellationToken ct);

    ValueTask Store(string subscriptionId, ulong position, CancellationToken ct);
}