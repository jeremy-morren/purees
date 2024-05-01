using System.Threading.Tasks.Dataflow;
using Marten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.EventBus;

namespace PureES.EventStores.Marten.Subscriptions;

internal class MartenSubscriptionToAll : IMartenEventStoreSubscription
{
    private readonly MartenEventsListener _eventsListener;
    private readonly IEventBus _eventBus;
    
    public MartenSubscriptionToAll(MartenEventSerializer serializer,
        IServiceProvider services,
        ILoggerFactory? loggerFactory = null)
    {
        _eventBus = new EventBus.EventBus(services, loggerFactory?.CreateLogger<EventBus.EventBus>());
        _eventsListener = new MartenEventsListener(serializer);
        _eventsListener.LinkTo(_eventBus, new DataflowLinkOptions() { PropagateCompletion = true });
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _eventsListener.Complete();
        return Task.WhenAny(Task.Delay(-1, cancellationToken), _eventBus.Completion);
    }

    public IDocumentSessionListener Listener => _eventsListener;
}