using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using PureES.EventBus;

namespace PureES.EventStore.InMemory.Subscription;

internal class InMemoryEventStoreSubscriptionToAll : IInMemoryEventStoreSubscription
{
    private readonly TransformManyBlock<List<EventRecord>, EventEnvelope> _target;
    private readonly IEventBus _eventBus;

    public InMemoryEventStoreSubscriptionToAll(
        IServiceProvider services,
        InMemoryEventStoreSerializer serializer,
        ILoggerFactory? loggerFactory = null)
    {
        _eventBus = new EventBus.EventBus(services, loggerFactory?.CreateLogger<EventBus.EventBus>());
        
        //Load records, and publish to event block
        
        _target = new TransformManyBlock<List<EventRecord>, EventEnvelope>(
            records => records.Select(serializer.Deserialize),
            new ExecutionDataflowBlockOptions()
            {
                BoundedCapacity = DataflowBlockOptions.Unbounded,
                
                //Ensure ordered
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = true
            });
        _target.LinkTo(_eventBus, new DataflowLinkOptions() { PropagateCompletion = true });
    }
    
    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct)
    {
        _target.Complete();
        return Task.WhenAny(Task.Delay(-1, ct), _eventBus.Completion);
    }

    public void AfterCommit(List<EventRecord> records)
    {
        if (!_target.Post(records))
            throw new InvalidOperationException("Cannot handle events after subscription stopped");
    }
}