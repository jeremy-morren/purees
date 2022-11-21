using System.Collections.Concurrent;

namespace PureES.EventStoreDB.Subscriptions;

internal class InMemorySubscriptionCheckpointRepository : ISubscriptionCheckpointRepository
{
    private readonly ConcurrentDictionary<string, ulong> _checkpoints = new();

    public ValueTask<ulong?> Load(string subscriptionId, CancellationToken ct) =>
        new(_checkpoints.TryGetValue(subscriptionId, out var checkpoint) ? checkpoint : null);

    public ValueTask Store(string subscriptionId, ulong position, CancellationToken ct)
    {
        _checkpoints.AddOrUpdate(subscriptionId, position, (_, _) => position);

        return ValueTask.CompletedTask;
    }
}