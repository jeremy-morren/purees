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
    
    public MartenSubscriptionToAll(IOptionsFactory<EventBusOptions> optionsFactory,
        MartenEventSerializer serializer,
        IServiceProvider services,
        ILoggerFactory? loggerFactory = null)
    {
        var options = optionsFactory.Create(nameof(MartenSubscriptionToAll));
        _eventBus = new EventBus.EventBus(options, services, loggerFactory?.CreateLogger<EventBus.EventBus>());
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