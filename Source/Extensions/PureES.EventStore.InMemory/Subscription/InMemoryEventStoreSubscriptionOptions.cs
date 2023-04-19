using PureES.EventBus.DataFlow;

namespace PureES.EventStore.InMemory.Subscription;

public class InMemoryEventStoreSubscriptionOptions
{
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    
    internal EventStreamBlockOptions GetWorkerOptions() => new()
    {
        MaxDegreeOfParallelism = MaxDegreeOfParallelism,
        BoundedCapacity = int.MaxValue
    };
}