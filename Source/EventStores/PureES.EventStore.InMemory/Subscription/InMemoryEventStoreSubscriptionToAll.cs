using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.EventBus;
using PureES.EventStore.InMemory.Serialization;

namespace PureES.EventStore.InMemory.Subscription;

internal class InMemoryEventStoreSubscriptionToAll : IHostedService, IEventStoreSubscription
{
    private readonly ITargetBlock<EventRecord> _targetBlock;
    
    private readonly IEventBus _eventBus;
    private readonly InMemoryEventStoreSerializer _serializer;

    private readonly BlockingCollection<EventRecord> _events = new();

    public InMemoryEventStoreSubscriptionToAll(
        IServiceProvider services,
        InMemoryEventStoreSerializer serializer,
        IOptionsFactory<InMemoryEventStoreSubscriptionOptions> optionsFactory,
        IEventHandlersCollection eventHandlersCollection,
        ILoggerFactory? loggerFactory = null)
    {
        _serializer = serializer;

        var options = optionsFactory.Create(nameof(InMemoryEventStoreSubscriptionToAll));

        //Ensure publish method below succeeds
        options.BufferSize = DataflowBlockOptions.Unbounded;
        options.MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded;

        _eventBus = new EventBus.EventBus(options.EventBusOptions,
            services,
            eventHandlersCollection,
            loggerFactory?.CreateLogger<EventBus.EventBus>());
        
        //Note: EventBus has buffer limitations, however we cannot block the Publish method
        //Therefore we have another block that buffers

        var target = new TransformBlock<EventRecord, EventEnvelope>(e => _serializer.Deserialize(e),
            new ExecutionDataflowBlockOptions()
            {
                BoundedCapacity = DataflowBlockOptions.Unbounded,
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = true //Important
            });
        target.LinkTo(_eventBus, new DataflowLinkOptions()
        {
            PropagateCompletion = true
        });
        _targetBlock = target;
    }

    //Note: access is synchronized by EventStore
    public void Publish(IEnumerable<EventRecord> envelopes)
    {
        if (!envelopes.All(_targetBlock.Post))
            throw new InvalidOperationException("Failed to schedule event");
    }

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken _)
    {
        _targetBlock.Complete();
        return _eventBus.Completion;
    }
}