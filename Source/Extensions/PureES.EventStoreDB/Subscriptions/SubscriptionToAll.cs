using System.Threading.Tasks.Dataflow;
using EventStore.Client;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.EventBus;
using PureES.EventStoreDB.Serialization;

namespace PureES.EventStoreDB.Subscriptions;

internal class SubscriptionToAll : BackgroundService, ISubscription
{
    private readonly IEventBus _eventBus;
    private readonly IEventStoreDBSerializer _dbSerializer;
    private readonly global::EventStore.Client.EventStoreClient _eventStoreClient;
    private readonly ISubscriptionCheckpointRepository _checkpointRepository;
    private readonly ILogger<SubscriptionToAll> _logger;
    private readonly SubscriptionOptions _options;

    public SubscriptionToAll(
        EventStoreClient eventStoreClient,
        IEventBus eventBus,
        IEventStoreDBSerializer dbSerializer,
        ISubscriptionCheckpointRepository checkpointRepository,
        ILogger<SubscriptionToAll> logger,
        IOptions<SubscriptionOptions> options)
    {
        _eventBus = eventBus;
        _dbSerializer = dbSerializer;
        _eventStoreClient = eventStoreClient;
        _checkpointRepository = checkpointRepository;
        _logger = logger;
        _options = options.Value;
    }
    
    private string SubscriptionId => _options.SubscriptionId;

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
            {
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

    private async Task SubscribeToAll(CancellationToken stoppingToken)
    {
        var checkpoint = await _checkpointRepository.Load(SubscriptionId, stoppingToken);
        
        var start = checkpoint == null
            ? FromAll.Start
            : FromAll.After(new Position(checkpoint.Value, checkpoint.Value));

        //This will ensure that the events are published 1 at a time (in the order that they were received)
        var block = new ActionBlock<EventEnvelope>(
            e => _eventBus.Publish(e, stoppingToken),
            new ExecutionDataflowBlockOptions()
            {
                CancellationToken = stoppingToken,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        using var subscription = await _eventStoreClient.SubscribeToAllAsync(
            start,
            (_, __, ___) => HandleEvent(block, _, __, ___),
            _options.ResolveLinkTos,
            HandleDrop,
            _options.FilterOptions,
            null,
            stoppingToken);
        
        _logger.LogInformation("Subscription {@Subscription} to all started", 
            new
            {
                ServerId = subscription.SubscriptionId,
                ClientId = SubscriptionId
            });
        
        //Wait for stop (from either drop or shutdown)
        WaitHandle.WaitAny(new[] { stoppingToken.WaitHandle, _droppedToken.Token.WaitHandle });
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

    private async Task HandleEvent(ITargetBlock<EventEnvelope> eventHandler,
        StreamSubscription subscription, 
        ResolvedEvent resolvedEvent, 
        CancellationToken ct)
    {
        try
        {
            if (IsEventWithEmptyData(resolvedEvent)) return;
            
            if (EventStoreSubscriptionCheckpointRepository.IsCheckpointEvent(resolvedEvent.Event))
            {
                _logger.LogDebug("Checkpoint event, ignoring");
                return;
            }

            var envelope = _dbSerializer.Deserialize(resolvedEvent.Event);

            //Publish event to event bus
            await eventHandler.SendAsync(envelope, ct);

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
