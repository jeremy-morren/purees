using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.EventBus;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore.Subscriptions;

internal class EfCoreEventStoreSubscriptionToAll : IEfCoreEventStoreSubscription
{
    private readonly EfCoreEventSerializer _serializer;
    private readonly IEventBus _eventBus;
    private readonly TransformBlock<EventStoreEvent, EventEnvelope> _handler;

    public EfCoreEventStoreSubscriptionToAll(
        EfCoreEventSerializer serializer,
        IServiceProvider services,
        IOptions<PureESOptions> options,
        ILoggerFactory? loggerFactory = null)
    {
        _serializer = serializer;
        _eventBus = new EventBus.EventBus(services,
            options.Value.EventBusOptions,
            loggerFactory?.CreateLogger<EventBus.EventBus>());

        _handler = new TransformBlock<EventStoreEvent, EventEnvelope>(
            DeserializeEvent,
            new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true
            });
        _handler.LinkTo(_eventBus, new DataflowLinkOptions()
        {
            PropagateCompletion = true
        });
    }

    public void OnEventsWritten(IEnumerable<EventStoreEvent> events)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var @event in events)
            if (!_handler.Post(@event))
                throw new InvalidOperationException("Failed to post event to handler");
    }

    private EventEnvelope DeserializeEvent(EventStoreEvent e) =>
        new(e.StreamId,
            (uint)e.StreamPos,
            e.Timestamp.UtcDateTime,
            _serializer.DeserializeEvent(e.StreamId, e.StreamPos, e.EventType, e.Event),
            _serializer.DeserializeMetadata(e.StreamId, e.StreamPos, e.Metadata));

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask; //noop

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _handler.Complete();
        return Task.WhenAny(Task.Delay(-1, cancellationToken), _eventBus.Completion);
    }
}