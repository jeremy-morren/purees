using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.CosmosDB.Serialization;
using PureES.EventBus;
using PureES.EventBus.DataFlow;

namespace PureES.CosmosDB.Subscription;

internal class CosmosEventStoreSubscriptionToAll : IHostedService, IEventStoreSubscription
{
    private readonly CosmosEventStoreClient _client;
    private readonly CosmosEventStoreSerializer _serializer;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CosmosEventStoreSubscriptionToAll> _logger;
    private readonly CosmosEventStoreSubscriptionOptions _options;

    public CosmosEventStoreSubscriptionToAll(CosmosEventStoreClient client,
        CosmosEventStoreSerializer serializer,
        IEventBus eventBus,
        ILogger<CosmosEventStoreSubscriptionToAll> logger,
        IOptionsFactory<CosmosEventStoreSubscriptionOptions> options)
    {
        _client = client;
        _serializer = serializer;
        _eventBus = eventBus;
        _logger = logger;
        _options = options.Create(nameof(CosmosEventStoreSubscriptionToAll));
    }

    private string LeaseContainerName => _options.LeaseContainerName ?? nameof(CosmosEventStoreSubscriptionToAll);

    private string ProcessorName => _options.ChangeFeedProcessorName ?? nameof(CosmosEventStoreSubscriptionToAll);


    public async Task<ChangeFeedProcessor> CreateProcessor(CancellationToken cancellationToken)
    {
        var eventStoreContainer = await _client.GetEventStoreContainerAsync(cancellationToken);

        if (_options.RestartFromBeginning)
        {
            //Delete lease container if exists
            var response = await eventStoreContainer.Database.GetContainer(LeaseContainerName)
                .DeleteContainerStreamAsync(cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode)
                _logger.LogDebug("Deleted lease container {LeaseContainer}", LeaseContainerName);
        }

        Container leaseContainer = await eventStoreContainer.Database
            .CreateContainerIfNotExistsAsync(id: LeaseContainerName,
                partitionKeyPath: "/partitionKey",
                throughput: _options.LeaseContainerThroughput,
                cancellationToken: cancellationToken);

        var builder = eventStoreContainer
            .GetChangeFeedProcessorBuilder<CosmosEvent>(processorName: ProcessorName,
                onChangesDelegate: HandleChangesAsync)
            .WithInstanceName(_options.InstanceName)
            .WithLeaseContainer(leaseContainer)
            .WithPollInterval(TimeSpan.FromSeconds(_options.PollInterval));

        if (_options.RestartFromBeginning)
            builder.WithStartTime(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));

        return builder.Build();
    }

    private EventStreamBlock? _worker;
    private ChangeFeedProcessor? _processor;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // see: https://github.com/dotnet/runtime/issues/36063
        await Task.Yield();
        
        try
        {
            _worker = new EventStreamBlock(_eventBus.Publish, _options.GetWorkerOptions());

            _processor = await CreateProcessor(cancellationToken);
            
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

        if (_worker != null)
        {
            _worker.Complete();
            await _worker.Completion;
        }
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
                if (!await _worker!.SendAsync(_serializer.Deserialize(@event), cancellationToken))
                    _logger.LogWarning("Event processor declined event {EventId}", @event.EventId);

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
}