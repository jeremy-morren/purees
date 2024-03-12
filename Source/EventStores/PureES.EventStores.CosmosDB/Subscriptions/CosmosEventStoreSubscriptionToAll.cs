using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PureES.EventBus;
using PureES.EventStores.CosmosDB.Serialization;

namespace PureES.EventStores.CosmosDB.Subscriptions;

internal class CosmosEventStoreSubscriptionToAll : ICosmosEventStoreSubscription
{
    private readonly CosmosEventStoreClient _client;
    private readonly CosmosEventStoreSerializer _serializer;
    private readonly ILogger<CosmosEventStoreSubscriptionToAll> _logger;
    private readonly CosmosEventStoreSubscriptionOptions _options;

    private readonly IEventBus _eventBus;

    public CosmosEventStoreSubscriptionToAll(CosmosEventStoreClient client,
        CosmosEventStoreSerializer serializer,
        IOptionsFactory<CosmosEventStoreSubscriptionOptions> optionsFactory,
        IServiceProvider services,
        ILogger<CosmosEventStoreSubscriptionToAll>? logger = null,
        ILoggerFactory? loggerFactory = null)
    {
        _client = client;
        _serializer = serializer;
        _logger = logger ?? NullLogger<CosmosEventStoreSubscriptionToAll>.Instance;
        
        _options = optionsFactory.Create(nameof(CosmosEventStoreSubscriptionToAll));

        _eventBus = new EventBus.EventBus(_options.EventBusOptions,
            services,
            loggerFactory?.CreateLogger<EventBus.EventBus>());
    }

    private string ProcessorName => _options.ChangeFeedProcessorName ?? nameof(CosmosEventStoreSubscriptionToAll);

    public ChangeFeedProcessor CreateProcessor()
    {
        var eventStoreContainer = _client.GetContainer();
        
        var leaseContainer = _client.GetLeaseContainer();

        var builder = eventStoreContainer
            .GetChangeFeedProcessorBuilder<CosmosEvent>(processorName: ProcessorName,
                onChangesDelegate: HandleChangesAsync)
            .WithInstanceName(_options.InstanceName)
            .WithLeaseContainer(leaseContainer)
            .WithPollInterval(_options.PollInterval)
            .WithLeaseAcquireNotification(OnLeaseAcquired)
            .WithLeaseReleaseNotification(OnLeaseReleased);
        
        return builder.Build();
    }

    private ChangeFeedProcessor? _processor;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // see: https://github.com/dotnet/runtime/issues/36063
        await Task.Yield();
        
        try
        {
            _logger.LogDebug("Starting CosmosDB change feed processor {ProcessorName}", ProcessorName);
            
            _processor = CreateProcessor();
            
            await _processor.StartAsync();

            _logger.LogInformation("CosmosDB change feed processor {ProcessorName} started", ProcessorName);
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Error starting CosmosDB change feed processor {ProcessorName}", ProcessorName);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            try
            {
                _logger.LogDebug("CosmosDB change feed processor {ProcessorName} stopping", ProcessorName);

                await _processor.StopAsync();
                
                _logger.LogInformation("CosmosDB change feed processor {ProcessorName} stopped", ProcessorName);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Error stopping CosmosDB change feed processor {ProcessorName}", ProcessorName);
                throw;
            }
        }
        
        _eventBus.Complete();
        await _eventBus.Completion;
    }

    /// <summary>
    /// The delegate receives batches of changes as they are generated in the change feed and can process them.
    /// </summary>
    private async Task HandleChangesAsync(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<CosmosEvent> changes,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Handling changes for lease {LeaseToken}. Change Feed request consumed {RequestCharge} RU", 
                context.LeaseToken, context.Headers.RequestCharge);

            // track any operation's Diagnostics that took long
            if (context.Diagnostics.GetClientElapsedTime() >= _options.ClientElapsedWarningThreshold)
                _logger.LogWarning("Change Feed request took longer than expected. Lease Token: {LeaseToken}, Diagnostics: {@Diagnostics}", 
                    context.LeaseToken, context.Diagnostics);

            foreach (var @event in changes)
                if (!await _eventBus.SendAsync(_serializer.Deserialize(@event), cancellationToken))
                    _logger.LogWarning("EventBus declined event {StreamId}/{StreamPosition}", 
                        @event.EventStreamId,
                        @event.EventStreamPosition);

            _logger.LogInformation("Published {Count} event(s) to EventBus. Lease: {LeaseToken}", 
                changes.Count,
                context.LeaseToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error handling changes for lease {LeaseToken}", context.LeaseToken);
            throw;
        }
    }

    private Task OnLeaseAcquired(string leaseName)
    {
        _logger.LogInformation("Acquired lease {LeaseName}", leaseName);
        return Task.CompletedTask;
    }
    
    private Task OnLeaseReleased(string leaseName)
    {
        _logger.LogInformation("Released lease {LeaseName}", leaseName);
        return Task.CompletedTask;
    }
}