using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.EventBus;
using PureES.EventBus.DataFlow;
using PureES.EventStore.InMemory.Serialization;

namespace PureES.EventStore.InMemory.Subscription;

internal class InMemoryEventStoreSubscriptionToAll : IEventStoreSubscription
{
    private readonly InMemoryEventStoreSerializer _serializer;
    private readonly ITargetBlock<EventEnvelope> _worker;

    public InMemoryEventStoreSubscriptionToAll(
        IEventBus eventBus,
        InMemoryEventStoreSerializer serializer,
        IOptionsFactory<InMemoryEventStoreSubscriptionOptions> options)
    {
        _serializer = serializer;
        _worker = new EventStreamBlock(eventBus.Publish,
            options.Create(nameof(InMemoryEventStoreSubscriptionToAll)).GetWorkerOptions());
    }

    public void Publish(IEnumerable<EventRecord> envelopes)
    {
        foreach (var e in envelopes)
            _worker.Post(_serializer.Deserialize(e));
    }

    public Task StartAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task StopAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}