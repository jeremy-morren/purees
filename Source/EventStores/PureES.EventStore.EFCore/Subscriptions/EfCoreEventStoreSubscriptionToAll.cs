using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PureES.EventBus;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore.Subscriptions;

internal class EfCoreEventStoreSubscriptionToAll : IEfCoreEventStoreSubscription
{
    private readonly IEventBus _eventBus;
    private readonly TransformBlock<EventStoreEvent, EventEnvelope> _handler;

    public EfCoreEventStoreSubscriptionToAll(
        EfCoreEventSerializer serializer,
        IServiceProvider services,
        ILoggerFactory? loggerFactory = null)
    {
        _eventBus = new EventBus.EventBus(services, loggerFactory?.CreateLogger<EventBus.EventBus>());

        _handler = new TransformBlock<EventStoreEvent, EventEnvelope>(e =>
                new EventEnvelope(e.StreamId,
                    e.StreamPos,
                    e.Timestamp.UtcDateTime,
                    serializer.DeserializeEvent(e.StreamId, e.StreamPos, e.EventType, e.Event),
                    serializer.DeserializeMetadata(e.StreamId, e.StreamPos, e.Metadata)),
            new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true
            });
        _handler.LinkTo(_eventBus, new DataflowLinkOptions() { PropagateCompletion = true });
    }

    public void OnEventsWritten(IEnumerable<EventStoreEvent> events)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var @event in events)
            if (!_handler.Post(@event))
                throw new InvalidOperationException("Failed to post event to handler");
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask; //noop

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _handler.Complete();
        return Task.WhenAny(Task.Delay(-1, cancellationToken), _eventBus.Completion);
    }
}