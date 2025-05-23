﻿using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Internal;
using PureES.EventStore.CosmosDB.Serialization;

namespace PureES.EventStore.CosmosDB;

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
    
    #region Write

    private List<TransactionalBatch> CreateTransactions(string streamId,
        uint startRevision,
        Container container,
        IEnumerable<UncommittedEvent> events,
        out uint revision) =>
        CreateTransactions(streamId, startRevision, container, events, _systemClock.UtcNow, out revision);
    
    private List<TransactionalBatch> CreateTransactions(string streamId,
        uint startRevision,
        Container container,
        IEnumerable<UncommittedEvent> events,
        DateTimeOffset timestamp,
        out uint revision)
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
            throw new ArgumentException("Input is empty", nameof(events)); //No events provided

        transactions.Add(transaction); //The last transaction 
        --revision; //The revision will have advanced too far

        return transactions;
    }

    private static async Task ExecuteBatch(string streamId, TransactionalBatch transaction, CancellationToken ct)
    {
        using var response = await transaction.ExecuteAsync(ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StreamAlreadyExistsException(streamId);
        if (!response.IsSuccessStatusCode)
            throw new CosmosException(response.ErrorMessage, 
                response.StatusCode, 
                (int)response.First(r => !r.IsSuccessStatusCode).StatusCode, 
                response.ActivityId,
                response.RequestCharge);
    }


    public async Task<uint> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();

        var transactions = CreateTransactions(streamId, 0, container, events, out var revision);

        foreach (var transaction in transactions)
            await ExecuteBatch(streamId, transaction, cancellationToken);

        return revision;
    }

    public Task<uint> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken) => 
        Create(streamId, [@event], cancellationToken);
    
    public async Task<uint> Append(string streamId, uint expectedRevision, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();
        await CheckRevision(container, streamId, expectedRevision, cancellationToken);
        
        var transactions = CreateTransactions(streamId, expectedRevision + 1, container, events, out var revision);
        foreach (var transaction in transactions)
            await ExecuteBatch(streamId, transaction, cancellationToken);
        
        return revision;
    }
    
    private static async Task CheckRevision(Container container, 
        string streamId,
        uint expectedRevision,
        CancellationToken cancellationToken)
    {
        var queryDef = new QueryDefinition(
                "select TOP 1 value c.eventStreamPosition from c where c.eventStreamId = @streamId order by c.eventStreamPosition desc")
            .WithParameter("@streamId", streamId);
        using var iterator = container.GetItemQueryIterator<uint?>(queryDef,
            requestOptions: new QueryRequestOptions()
            {
                PartitionKey = new PartitionKey(streamId)
            });
        var result = await iterator.ReadNextAsync(cancellationToken);
        var actual = result.Resource.FirstOrDefault() ?? throw new StreamNotFoundException(streamId);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
    }

    public async Task<uint> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();
        var revision = await GetRevision(streamId, cancellationToken);
        
        var transactions = CreateTransactions(streamId, revision + 1, container, events, out revision);
        foreach (var transaction in transactions)
            await ExecuteBatch(streamId, transaction, cancellationToken);
        
        return revision;
    }

    public Task<uint> Append(string streamId, uint expectedRevision, UncommittedEvent @event,
        CancellationToken cancellationToken)
        => Append(streamId, expectedRevision, [@event], cancellationToken);

    public async Task<uint> Append(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();
        var revision = await GetRevision(streamId, cancellationToken) + 1;

        await container.CreateItemAsync(_serializer.Serialize(@event, streamId, revision, _systemClock.UtcNow), cancellationToken: cancellationToken);
        return revision;
    }

    public async Task SubmitTransaction(IReadOnlyList<UncommittedEventsList> transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Count == 0)
            return;
        
        var container = _client.GetContainer();
        
        const string sql = "select c.eventStreamId, max(c.eventStreamPosition) as position from c where ARRAY_CONTAINS(@streams, c.eventStreamId, false) group by c.eventStreamId";

        var query = new QueryDefinition(sql)
            .WithParameter("@streams", transaction.Select(s => s.StreamId));

        var current = new Dictionary<string, uint>(capacity: transaction.Count);
        using (var iterator = container.GetItemQueryIterator<StreamPosition>(query))
            while (iterator.HasMoreResults)
                foreach (var i in await iterator.ReadNextAsync(cancellationToken))
                    current.Add(i.EventStreamId, i.Position);

        var batches = new Dictionary<string, List<TransactionalBatch>>();
        var exceptions = new List<Exception>();

        var ts = _systemClock.UtcNow;
        foreach (var (streamId, expectedRevision, events) in transaction)
        {
            if (current.TryGetValue(streamId, out var actual))
            {
                if (expectedRevision == null)
                {
                    exceptions.Add(new StreamAlreadyExistsException(streamId));
                }
                else if (expectedRevision != actual)
                {
                    exceptions.Add(new WrongStreamRevisionException(streamId, expectedRevision.Value, actual));
                }
                else if (events.Count > 0)
                {
                    var transactions = CreateTransactions(streamId, actual + 1, container, events, out _);
                    batches.Add(streamId, transactions);
                }
            }
            else
            {
                if (expectedRevision != null)
                {
                    exceptions.Add(new StreamNotFoundException(streamId));
                }
                else if (events.Count > 0)
                {
                    var transactions = CreateTransactions(streamId, 0, container, events, ts, out _);
                    batches.Add(streamId, transactions);
                }
            }
        }

        switch (exceptions.Count)
        {
            case 0:
                break;
            case 1:
                throw exceptions[0];
            default:
                throw new EventsTransactionException(exceptions);
        }
        

        if (batches.Count == 0) return;

        //CosmosDB requires a partition key for all operations
        //Therefore we can't make this atomic
        await Task.WhenAll(batches.Select(Execute));
        
        return;

        async Task Execute(KeyValuePair<string, List<TransactionalBatch>> list)
        {
            foreach (var t in list.Value)
                await ExecuteBatch(list.Key, t, cancellationToken);
        }
    }

    public async Task<bool> Exists(string streamId, CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();
        //Check event 0 exists
        using var response = await container.ReadItemStreamAsync($"{streamId}|0", 
            new PartitionKey(streamId),
            cancellationToken: cancellationToken);
        return response.IsSuccessStatusCode;
    }
    
    #endregion
    
    #region Read
    
    private FeedIterator<CosmosEvent> CreateIterator(string streamId, QueryDefinition queryDefinition)
    {
        var container = _client.GetContainer();
        return container.GetItemQueryIterator<CosmosEvent>(queryDefinition,
            requestOptions: new QueryRequestOptions()
            {
                PartitionKey = new PartitionKey(streamId)
            });
    }

    public async Task<uint> GetRevision(string streamId, CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();
        var queryDef = new QueryDefinition(
                "select TOP 1 value c.eventStreamPosition from c where c.eventStreamId = @streamId order by c.eventStreamPosition desc")
            .WithParameter("@streamId", streamId);
        using var iterator = container.GetItemQueryIterator<uint?>(queryDef,
            requestOptions: new QueryRequestOptions()
            {
                PartitionKey = new PartitionKey(streamId)
            });
        var result = await iterator.ReadNextAsync(cancellationToken);
        return result.Resource.FirstOrDefault() ?? throw new StreamNotFoundException(streamId);
    }

    public async Task<uint> GetRevision(string streamId, uint expectedRevision, CancellationToken cancellationToken)
    {
        var actual = await GetRevision(streamId, cancellationToken);
        if (actual == expectedRevision) return actual;
        throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();
        
        var queryDef = direction switch
        {
            Direction.Forwards => new QueryDefinition(
                "select * from c order by c._ts, c.created, c.eventStreamId, c.eventStreamPosition"),
            Direction.Backwards => new QueryDefinition(
                "select * from c order by c._ts desc, c.created desc, c.eventStreamId desc, c.eventStreamPosition desc"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }
    
    public async IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, 
        uint maxCount,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();

        var queryDef = direction switch
        {
            Direction.Forwards => new QueryDefinition(
                "select top @count * from c order by c._ts, c.created, c.eventStreamId, c.eventStreamPosition"),
            Direction.Backwards => new QueryDefinition(
                "select top @count * from c order by c._ts desc, c.created desc, c.eventStreamId desc, c.eventStreamPosition desc"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        
        queryDef = queryDef.WithParameter("@count", maxCount);

        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }

    private async IAsyncEnumerable<EventEnvelope> ReadInternal(Direction direction,
        string streamId, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var queryDef = direction switch
        {
            Direction.Forwards => new QueryDefinition("select * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition"),
            Direction.Backwards => new QueryDefinition("select * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition desc"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        
        queryDef = queryDef.WithParameter("@streamId", streamId);

        using var iterator = CreateIterator(streamId, queryDef);
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

    public IEventStoreStream Read(Direction direction, string streamId,  CancellationToken cancellationToken) =>
        new CosmosDBEventStoreStream(direction, streamId, ReadInternal(direction, streamId, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadInternal(Direction direction,
        string streamId, 
        uint expectedRevision, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var queryDef = direction switch 
        {
            Direction.Forwards => new QueryDefinition(
                "select * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition"),
            Direction.Backwards => new QueryDefinition(
                "select * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition desc"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        
        queryDef = queryDef.WithParameter("@streamId", streamId);

        using var iterator = CreateIterator(streamId, queryDef);
        var revision = uint.MaxValue;
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
            {
                yield return _serializer.Deserialize(e);
                ++revision;
            }
        }

        if (revision == uint.MaxValue)
            throw new StreamNotFoundException(streamId);
        if (expectedRevision != revision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, revision);
    }

    public IEventStoreStream Read(Direction direction, string streamId, uint expectedRevision, CancellationToken cancellationToken) =>
        new CosmosDBEventStoreStream(direction, streamId, ReadInternal(direction, streamId, expectedRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadInternal(Direction direction,
        string streamId,
        uint startRevision, 
        uint expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, expectedRevision);

        var queryDef = direction switch 
        {
            Direction.Forwards => new QueryDefinition(
                "select * from c where c.eventStreamId = @streamId and c.eventStreamPosition >= @startPos ORDER BY c.eventStreamPosition"),
            Direction.Backwards => new QueryDefinition(
                "select * from c where c.eventStreamId = @streamId and c.eventStreamPosition >= @startPos ORDER BY c.eventStreamPosition desc"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        queryDef = queryDef.WithParameter("@streamId", streamId)
            .WithParameter("@startPos", startRevision);

        using var iterator = CreateIterator(streamId, queryDef);
        var revision = startRevision;
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
            {
                yield return _serializer.Deserialize(e);
                ++revision;
            }
        }
        
        if (revision == startRevision)
            throw new StreamNotFoundException(streamId);
        --revision; //The revision will have advanced too far
        if (expectedRevision != revision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, revision);
    }

    public IEventStoreStream Read(Direction direction, string streamId, uint startRevision, uint expectedRevision, CancellationToken cancellationToken) =>
        new CosmosDBEventStoreStream(direction, streamId, ReadInternal(direction, streamId, startRevision, expectedRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadPartialInternal(Direction direction,
        string streamId,
        uint count, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var queryDef = direction switch
        {
            Direction.Forwards => new QueryDefinition(
                "select TOP @count * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition"),
            Direction.Backwards => new QueryDefinition(
                "select TOP @count * from c where c.eventStreamId = @streamId ORDER BY c.eventStreamPosition desc"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        
        queryDef = queryDef
            .WithParameter("@streamId", streamId)
            .WithParameter("@count", count);

        using var iterator = CreateIterator(streamId, queryDef);
        var read = 0u;
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
            {
                yield return _serializer.Deserialize(e);
                ++read;
                if (count == read)
                    yield break;
            }
        }

        if (read == 0)
            throw new StreamNotFoundException(streamId);
        if (read < count)
            throw new WrongStreamRevisionException(streamId, count - 1, read - 1);
    }

    public IEventStoreStream ReadPartial(Direction direction, string streamId, uint count, CancellationToken cancellationToken) =>
        new CosmosDBEventStoreStream(direction, streamId, ReadPartialInternal(direction, streamId, count, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadSliceInternal(string streamId,
        uint startRevision, 
        uint endRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, endRevision);

        var queryDef = new QueryDefinition(
            "select * from c where c.eventStreamId = @streamId and (c.eventStreamPosition = 0 or (c.eventStreamPosition >= @start and c.eventStreamPosition <= @end)) ORDER BY c.eventStreamPosition");
        
        queryDef = queryDef
            .WithParameter("@streamId", streamId)
            .WithParameter("@start", startRevision)
            .WithParameter("@end", endRevision);

        using var iterator = CreateIterator(streamId, queryDef);
        var exists = false;
        uint? actual = null;
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
            {
                exists = true;
                var env = _serializer.Deserialize(e);
                if (env.StreamPosition == 0 && startRevision != 0)
                    continue;
                yield return env;
                actual = env.StreamPosition;
            }
        }

        if (!exists)
            throw new StreamNotFoundException(streamId);
        
        if (actual == null || actual.Value < endRevision)
            throw new WrongStreamRevisionException(streamId, endRevision, 
                await GetRevision(streamId, cancellationToken));
    }

    public IEventStoreStream ReadSlice(string streamId, uint startRevision, uint endRevision, CancellationToken cancellationToken) =>
        new CosmosDBEventStoreStream(Direction.Forwards, streamId, ReadSliceInternal(streamId, startRevision, endRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadSliceInternal(string streamId,
        uint startRevision, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var queryDef = new QueryDefinition(
            "select * from c where c.eventStreamId = @streamId and (c.eventStreamPosition = 0 OR c.eventStreamPosition >= @start) ORDER BY c.eventStreamPosition");
        
        queryDef = queryDef
            .WithParameter("@streamId", streamId)
            .WithParameter("@start", startRevision);

        using var iterator = CreateIterator(streamId, queryDef);
        var exists = false;
        uint? actual = null;
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
            {
                exists = true;
                var env = _serializer.Deserialize(e);
                if (env.StreamPosition == 0 && startRevision != 0) continue;
                yield return env;
                actual = env.StreamPosition;
            }
        }

        if (!exists)
            throw new StreamNotFoundException(streamId);
        if (actual == null)
            throw new WrongStreamRevisionException(streamId, startRevision, 
                await GetRevision(streamId, cancellationToken));
    }

    public IEventStoreStream ReadSlice(string streamId, uint startRevision, CancellationToken cancellationToken) =>
        new CosmosDBEventStoreStream(Direction.Forwards, streamId, ReadSliceInternal(streamId, startRevision, cancellationToken));

    public async IAsyncEnumerable<IEventStoreStream> ReadMany(Direction direction,
        IEnumerable<string> streams, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var list = streams.ToList();

        var queryDef = direction switch
        {
            Direction.Forwards => new QueryDefinition(
                "select * from c where ARRAY_CONTAINS(@streams, c.eventStreamId, false) ORDER BY c.eventStreamId, c.eventStreamPosition"),
            Direction.Backwards => new QueryDefinition(
                "select * from c where ARRAY_CONTAINS(@streams, c.eventStreamId, false) ORDER BY c.eventStreamId desc, c.eventStreamPosition desc"),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        queryDef = queryDef.WithParameter("@streams", list);
            
        var container = _client.GetContainer();

        var read = new HashSet<string>();
        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);

        var stream = new List<EventEnvelope>();

        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
            {
                read.Add(e.EventStreamId);
                var item = _serializer.Deserialize(e);
                if (stream.Count == 0 || item.StreamId == stream[0].StreamId)
                {
                    stream.Add(item);
                    continue;
                }
                //We have started a new stream
                yield return new CosmosDBEventStoreStream(direction, item.StreamId, stream.ToAsyncEnumerable());
                stream = [item];
            }
        }

        if (stream.Count > 0)
        {
            //Return final stream
            yield return new CosmosDBEventStoreStream(direction, stream[0].StreamId, stream.ToAsyncEnumerable());
        }

        var missing = list.Except(read)
            .Select(id => new StreamNotFoundException(id))
            .ToList();
        //Check for missing streams
        switch (missing.Count)
        {
            case 0:
                break;
            case 1:
                throw missing[0];
            default:
                throw new AggregateException(missing);
        }
    }
    
    public async IAsyncEnumerable<IEventStoreStream> ReadMany(Direction direction,
        IAsyncEnumerable<string> streams, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var list = await streams.ToListAsync(cancellationToken);
        await foreach (var stream in ReadMany(direction, list, cancellationToken))
            yield return stream;
    }
    
    #endregion
    
    #region By Event Type
    
    public async IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, 
        Type[] eventTypes, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();

        var query = "select * from c where SetIntersect(c.eventTypes, @eventTypes) ORDER BY ";
        query += direction switch
        {
            Direction.Forwards => "c._ts, c.created, c.eventStreamId, c.eventStreamPosition",
            Direction.Backwards => "c._ts desc, c.created desc, c.eventStreamId desc, c.eventStreamPosition desc",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        var queryDef = new QueryDefinition(query)
            .WithParameter("@eventTypes", GetTypeNames(eventTypes));

        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }
    
    public async IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, 
        Type[] eventTypes, 
        uint maxCount,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();

        var query = "select top @count * from c where SetIntersect(c.eventTypes, @eventTypes) ORDER BY ";
        query += direction switch
        {
            Direction.Forwards => "c._ts, c.created, c.eventStreamId, c.eventStreamPosition",
            Direction.Backwards => "c._ts desc, c.created desc, c.eventStreamId desc, c.eventStreamPosition desc",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        var queryDef = new QueryDefinition(query)
            .WithParameter("@eventTypes", GetTypeNames(eventTypes))
            .WithParameter("@count", maxCount);

        using var iterator = container.GetItemQueryIterator<CosmosEvent>(queryDef);
        while (iterator.HasMoreResults)
        {
            var result = await iterator.ReadNextAsync(cancellationToken);
            foreach (var e in result)
                yield return _serializer.Deserialize(e);
        }
    }
    
    #endregion
    
    #region Count
    
    public async Task<uint> Count(CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();

        var queryDef = new QueryDefinition("select value count(1) from c");

        using var iterator = container.GetItemQueryIterator<uint>(queryDef);
        var result = await iterator.ReadNextAsync(cancellationToken);
        return result.Single();
    }
    
    public async Task<uint> CountByEventType(Type[] eventTypes, CancellationToken cancellationToken)
    {
        var container = _client.GetContainer();

        const string query = "select value count(1) from c where SetIntersect(c.eventTypes, @eventTypes)";
        var queryDef = new QueryDefinition(query).WithParameter("@eventType", GetTypeNames(eventTypes));

        using var iterator = container.GetItemQueryIterator<uint>(queryDef);
        var result = await iterator.ReadNextAsync(cancellationToken);
        return result.Single();
    }

    #endregion
    
    private HashSet<string> GetTypeNames(Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        return eventTypes
            .SelectMany(t => _typeMap.GetTypeNames(t))
            .Distinct()
            .ToHashSet();
    }
}