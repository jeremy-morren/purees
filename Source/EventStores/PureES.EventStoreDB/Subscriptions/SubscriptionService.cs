using System.Threading.Tasks.Dataflow;
using EventStore.Client;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.EventBus;
// ReSharper disable MemberCanBePrivate.Global

namespace PureES.EventStoreDB.Subscriptions;

internal abstract class SubscriptionService : BackgroundService, IEventStoreSubscription
{
    private readonly IEventStoreDBSubscriptionCheckpointRepository _checkpointRepository;
    private readonly IEventBus _eventBus;
    private readonly EventStoreClient _eventStoreClient;
    private readonly ILogger _logger;
    private readonly SubscriptionOptions _options;
    private readonly EventStoreDBSerializer _serializer;

    protected SubscriptionService(
        EventStoreClient eventStoreClient,
        EventStoreDBSerializer serializer,
        IOptionsFactory<SubscriptionOptions> optionsFactory,
        
        IServiceProvider services,
        IEventHandlersCollection eventHandlersCollection,
        
        ILoggerFactory? loggerFactory = null)
    {
        _serializer = serializer;
        _eventStoreClient = eventStoreClient;
        _logger = loggerFactory?.CreateLogger(GetType()) ?? NullLogger.Instance;
        _options = optionsFactory.Create(GetType().Name);

        if (_options.CheckpointToEventStoreDB)
            _checkpointRepository = new EventStoreDBSubscriptionCheckpointRepository(eventStoreClient);
        else
            _checkpointRepository = new InMemoryEventStoreDBSubscriptionCheckpointRepository();

        _eventBus = new EventBus.EventBus(_options.EventBusOptions, 
            services,
            eventHandlersCollection,
            loggerFactory?.CreateLogger<EventBus.EventBus>());
    }

    protected string SubscriptionId => _options.SubscriptionId;

    #region Subscription

    private CancellationTokenSource _droppedToken = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // see: https://github.com/dotnet/runtime/issues/36063
        await Task.Yield();

        _logger.LogInformation("Starting {SubscriptionId} subscription to all", SubscriptionId);

        try
        {
            while (true)
                try
                {
                    await SubscribeToAll(stoppingToken);
                    if (_droppedToken.IsCancellationRequested)
                    {
                        //Subscription was dropped. Wait then continue loop
                        //We don't need to log, it was already logged in HandleDrop
                        await Task.Delay(_options.ResubscribeDelay, stoppingToken);
                        _droppedToken = new CancellationTokenSource(); //Reset token
                    }
                    else
                    {
                        //We are stopping
                        _logger.LogInformation("Stopped {SubscriptionId} subscription to all", SubscriptionId);
                        break;
                    }
                }
                catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
                {
                    _logger.LogWarning(e, "Error starting subscription {SubscriptionId} to all", SubscriptionId);
                    await Task.Delay(_options.ResubscribeDelay, stoppingToken);
                }
        }
        catch (OperationCanceledException e)
        {
            _logger.LogDebug(e, "Subscription {SubscriptionId} canceled", SubscriptionId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Subscription {SubscriptionId} errored unexpectedly", SubscriptionId);
            throw;
        }
    }

    protected abstract Task<StreamSubscription> Subscribe(EventStoreClient client,
        FromAll start,
        Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
        bool resolveLinkTos,
        Action<StreamSubscription, SubscriptionDroppedReason, Exception?>? subscriptionDropped,
        SubscriptionFilterOptions? filterOptions,
        CancellationToken cancellationToken);

    private async Task SubscribeToAll(CancellationToken stoppingToken)
    {
        var checkpoint = await _checkpointRepository.Load(SubscriptionId, stoppingToken);

        var start = checkpoint == null
            ? FromAll.Start
            : FromAll.After(new Position(checkpoint.Value, checkpoint.Value));

        using var subscription = await Subscribe(_eventStoreClient,
            start,
            HandleEvent,
            true,
            HandleDrop,
            _options.FilterOptions,
            stoppingToken);

        _logger.LogInformation("Subscription {@Subscription} to all started",
            new
            {
                ServerId = subscription.SubscriptionId,
                ClientId = SubscriptionId
            });

        //Wait for stop (from either drop or shutdown)
        WaitHandle.WaitAny(new[] {stoppingToken.WaitHandle, _droppedToken.Token.WaitHandle});
    }

    private void HandleDrop(StreamSubscription subscription, SubscriptionDroppedReason reason, Exception? exception)
    {
        if (reason == SubscriptionDroppedReason.Disposed)
            return;

        _logger.LogWarning(exception, "Subscription to all {@Subscription} dropped with {Reason}",
            new
            {
                ServerId = subscription.SubscriptionId,
                ClientId = SubscriptionId
            },
            reason);

        _droppedToken.Cancel();
    }

    #endregion

    #region Publish

    private async Task HandleEvent(StreamSubscription subscription,
        ResolvedEvent resolvedEvent,
        CancellationToken ct)
    {
        try
        {
            if (IsEventWithEmptyData(resolvedEvent)) return;

            if (EventStoreDBSubscriptionCheckpointRepository.IsCheckpointEvent(resolvedEvent.Event))
            {
                _logger.LogDebug("Checkpoint event, ignoring");
                return;
            }

            var envelope = _serializer.Deserialize(resolvedEvent.Event);

            //Publish event to event bus
            if (!await _eventBus.SendAsync(envelope, ct))
                _logger.LogWarning("EventBus declined event {EventId}", envelope.EventId);

            await _checkpointRepository.Store(SubscriptionId, resolvedEvent.Event.Position.CommitPosition, ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error consuming event");
        }
    }

    private bool IsEventWithEmptyData(ResolvedEvent resolvedEvent)
    {
        if (resolvedEvent.Event.Data.Length != 0) return false;

        _logger.LogDebug("Event without data received");
        return true;
    }

    #endregion
}