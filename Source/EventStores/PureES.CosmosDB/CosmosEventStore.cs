using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Internal;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.CosmosDB.Serialization;

namespace PureES.CosmosDB;

internal class CosmosEventStore : IEventStore
{
    private readonly CosmosEventStoreClient _client;
    private readonly CosmosEventStoreSerializer _serializer;
    private readonly IEventTypeMap _typeMap;
    private readonly ISystemClock _systemClock;

    public CosmosEventStore(CosmosEventStoreClient client,
        CosmosEventStoreSerializer serializer,
        IEventTypeMap typeMap,
        ISystemClock systemClock)
    {
        _client = client;
        _serializer = serializer;
        _typeMap = typeMap;
        _systemClock = systemClock;
    }

    private List<TransactionalBatch> CreateTransactions(string streamId,
        ulong startRevision,
        Container container,
        IEnumerable<UncommittedEvent> events,
        out ulong revision)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));
        
        //CosmosDB has a transaction maximum request size of 2MB
        //And a maximum count of 100
        //See https://learn.microsoft.com/en-us/azure/cosmos-db/concepts-limits#per-request-limits
        
        
        //Therefore we will create transactions that are under 2MB and no more than 100 items
        //Unfortunately, this means that the insert of huge event lists is not atomic
        //A better solution would be good

        const int maxBodySize = 2 * 1024 * 1024;
        const int maxCount = 100;

        var transactions = new List<TransactionalBatch>();

        var partitionKey = new PartitionKey(streamId);
        revision = startRevision;
        
        var transaction = container.CreateTransactionalBatch(partitionKey);
        var transactionSize = 0;
        var transactionCount = 0;
        var timestamp = _systemClock.UtcNow;
        foreach (var @event in events)
        {
            var cosmosEvent = _serializer.Serialize(@event, streamId, revision++, timestamp);

            var stream = _client.Serializer.ToMemoryStream(cosmosEvent);
            
            //Create new transaction if necessary

            if (stream.Length > maxBodySize)
            {
                //This event is too large to fit in a single transaction
                throw new InvalidOperationException(
                    "Total event size cannot exceed 2MB. See https://learn.microsoft.com/en-us/azure/cosmos-db/concepts-limits#per-request-limits");
            }

            var length = (int) stream.Length;

            if (transactionSize + length > maxBodySize
                || transactionCount == maxCount)
            {
                //This event would cause the transaction to exceed the maximum body size
                //Or the maximum number of operations
                
                //Start a new one
                transactions.Add(transaction);
                transaction = container.CreateTransactionalBatch(partitionKey);
                transactionSize = 0;
                transactionCount = 0;
            }

            transaction.CreateItemStream(stream,
                new TransactionalBatchItemRequestOptions() {EnableContentResponseOnWrite = false});
            transactionSize += length;
            ++transactionCount;
        }

        if (transactionCount == 0)
            throw new ArgumentException("Input is empty", nameof(@events)); //No events provided

        transactions.Add(transaction); //The last transaction 
        --revision; //The revision will have advanced too far

        return transactions;
    }


    public async Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);

        var transactions = CreateTransactions(streamId, 0, container, events, out var revision);

        foreach (var transaction in transactions)
        {
            using var response = await transaction.ExecuteAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new StreamAlreadyExistsException(streamId, await GetRevision(streamId, cancellationToken));
            if (!response.IsSuccessStatusCode)
                throw new CosmosException(response.ErrorMessage, 
                    response.StatusCode, 
                    (int)response.First(r => !r.IsSuccessStatusCode).StatusCode, 
                    response.ActivityId,
                    response.RequestCharge);
        }

        return revision;
    }

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
        => Create(streamId, new[] {@event}, cancellationToken);
    
    public async Task<ulong> Append(string streamId, ulong expectedRevision, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);
        await CheckRevision(container, streamId, expectedRevision, cancellationToken);
        
        var transactions = CreateTransactions(streamId, expectedRevision + 1, container, events, out var revision);
        foreach (var transaction in transactions)
        {
            using var response = await transaction.ExecuteAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new CosmosException(response.ErrorMessage, 
                    response.StatusCode, 
                    (int)response.First(r => !r.IsSuccessStatusCode).StatusCode, 
                    response.ActivityId,
                    response.RequestCharge);
        }
        
        return revision;
    }
    
    private static async Task CheckRevision(Container container, 
        string streamId,
        ulong expectedRevision,
        CancellationToken cancellationToken)
    {
        var queryDef = new QueryDefinition(
                "select TOP 1 c.eventStreamPosition from c where c.eventStreamId = @streamId order by c.eventStreamPosition desc")
            .WithParameter("@streamId", streamId);
        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef,
            requestOptions: new QueryRequestOptions()
            {
                PartitionKey = new PartitionKey(streamId)
            });
        var result = await iterator.ReadNextAsync(cancellationToken);
        var actual = result.Resource.FirstOrDefault()?.EventStreamPosition ?? throw new StreamNotFoundException(streamId);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
    }


    public async Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);
        var revision = await GetRevision(streamId, cancellationToken);
        
        var transactions = CreateTransactions(streamId, revision + 1, container, events, out revision);
        foreach (var transaction in transactions)
        {
            using var response = await transaction.ExecuteAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new CosmosException(response.ErrorMessage, 
                    response.StatusCode, 
                    (int)response.First(r => !r.IsSuccessStatusCode).StatusCode, 
                    response.ActivityId,
                    response.RequestCharge);
        }
        
        return revision;
    }

    public Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event,
        CancellationToken cancellationToken)
        => Append(streamId, expectedRevision, new[] {@event}, cancellationToken);

    public async Task<ulong> Append(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);
        var revision = await GetRevision(streamId, cancellationToken) + 1;

        await container.CreateItemAsync(_serializer.Serialize(@event, streamId, revision, _systemClock.UtcNow), cancellationToken: cancellationToken);
        return revision;
    }

    public async Task<bool> Exists(string streamId, CancellationToken cancellationToken)
    {
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);
        //Check event 0 exists
        using var response = await container.ReadItemStreamAsync($"{streamId}|0", 
            new PartitionKey(streamId),
            cancellationToken: cancellationToken);
        return response.IsSuccessStatusCode;
    }
    
    private async Task<FeedIterator<CosmosEvent>> CreateIterator(string streamId, 
        QueryDefinition queryDefinition,
        CancellationToken cancellationToken)
    {
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);
        return container.GetItemQueryIterator<CosmosEvent>(queryDefinition,
            requestOptions: new QueryRequestOptions()
            {
                PartitionKey = new PartitionKey(streamId)
            });
    }

    public async Task<ulong> GetRevision(string streamId, CancellationToken cancellationToken)
    {
        var queryDef = new QueryDefinition(
                "select TOP 1 c.eventStreamPosition from c where c.eventStreamId = @stream order by c.eventStreamPosition desc")
            .WithParameter("@stream", streamId);

        using var iterator = await CreateIterator(streamId, queryDef, cancellationToken);
        var result = await iterator.ReadNextAsync(cancellationToken);
        return result.Resource.FirstOrDefault()?.EventStreamPosition ?? throw new StreamNotFoundException(streamId);
    }

    public async Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken cancellationToken)
    {
        var actual = await GetRevision(streamId, cancellationToken);
        if (actual == expectedRevision) return actual;
        throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadAll([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);

        var queryDef = new QueryDefinition("select * from c order by c._ts, c.created, c.eventStreamId, c.eventStreamPosition");

        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }

    public async IAsyncEnumerable<EventEnvelope> Read(string streamId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queryDef =
            new QueryDefinition("select * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition")
                .WithParameter("@streamId", streamId);

        using var iterator = await CreateIterator(streamId, queryDef, cancellationToken);
        if (!iterator.HasMoreResults)
            throw new StreamNotFoundException(streamId);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            if (result.Count == 0)
                throw new StreamNotFoundException(streamId);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }

    public async IAsyncEnumerable<EventEnvelope> Read(string streamId, ulong expectedRevision, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queryDef =
            new QueryDefinition("select * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition")
                .WithParameter("@streamId", streamId);

        using var iterator = await CreateIterator(streamId, queryDef, cancellationToken);
        var revision = ulong.MaxValue;
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
            {
                yield return _serializer.Deserialize(e);
                ++revision;
            }
        }

        if (revision == ulong.MaxValue)
            throw new StreamNotFoundException(streamId);
        if (expectedRevision != revision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, revision);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadPartial(string streamId, ulong requiredRevision, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (requiredRevision == ulong.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(requiredRevision));
        
        var queryDef =
            new QueryDefinition(
                    "select TOP @required * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition")
                .WithParameter("@streamId", streamId)
                .WithParameter("@required", requiredRevision + 1);

        using var iterator = await CreateIterator(streamId, queryDef, cancellationToken);
        var revision = ulong.MaxValue;
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
            {
                yield return _serializer.Deserialize(e);
                ++revision;
                if (requiredRevision == revision)
                    yield break;
            }
        }

        if (revision == ulong.MaxValue)
            throw new StreamNotFoundException(streamId);
        if (revision < requiredRevision)
            throw new WrongStreamRevisionException(streamId, requiredRevision, revision);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadByEventType(Type eventType, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);

        var queryDef =
            new QueryDefinition(
                    "select * from c where c.eventType = @eventType ORDER BY c._ts, c.created, c.eventStreamId, c.eventStreamPosition")
                .WithParameter("@eventType", _typeMap.GetTypeName(eventType));

        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }

    public async IAsyncEnumerable<EventEnvelope> ReadMany(IEnumerable<string> streams, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queryDef =
            new QueryDefinition(
                    "select * from c where ARRAY_CONTAINS(@streams, c.eventStreamId, false) ORDER BY c._ts, c.created, c.eventStreamId, c.eventStreamPosition")
                .WithParameter("@streams", streams);
        
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);
        
        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }

    public async IAsyncEnumerable<EventEnvelope> ReadMany(IAsyncEnumerable<string> streams, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var streamList = new List<string>();
        await foreach (var s in streams.WithCancellation(cancellationToken))
            streamList.Add(s);
        var queryDef =
            new QueryDefinition(
                    "select * from c where ARRAY_CONTAINS(@streams, c.eventStreamId, false) ORDER BY c._ts, c.created, c.eventStreamId, c.eventStreamPosition")
                .WithParameter("@streams", streamList);
        var container = await _client.GetEventStoreContainerAsync(cancellationToken);
        
        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }
}