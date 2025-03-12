using System.Runtime.CompilerServices;
using Marten;
using Marten.Exceptions;
using PureES.EventStore.Marten.CustomMethods;

namespace PureES.EventStore.Marten;

internal class MartenEventStore : IEventStore
{
    private readonly IDocumentStore _documentStore;
    private readonly IEventTypeMap _eventTypeMap;
    private readonly MartenEventSerializer _serializer;

    public MartenEventStore(IDocumentStore documentStore,
        IEventTypeMap eventTypeMap,
        MartenEventSerializer serializer)
    {
        _documentStore = documentStore;
        _eventTypeMap = eventTypeMap;
        _serializer = serializer;
    }

    private IQuerySession ReadSession() => _documentStore.QuerySession();

    private IDocumentSession WriteSession() => _documentStore.LightweightSession();

    public async Task<bool> Exists(string streamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        await using var session = ReadSession();
        return await session.Query<MartenEvent>()
            .Where(e => e.Id == $"{streamId}/0")
            .AnyAsync(token: cancellationToken);
    }

    public async Task<uint> GetRevision(string streamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        await using var session = ReadSession();
        return await CheckRevision(streamId, session, cancellationToken);
    }

    public async Task<uint> GetRevision(string streamId, uint expectedRevision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var revision = await GetRevision(streamId, cancellationToken);
        if (revision != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, revision);
        return revision;
    }
    
    private static async Task<uint> CheckRevision(string streamId, IQuerySession session, CancellationToken ct)
    {
        var pos = await session.Query<MartenEvent>()
            .Where(e => e.StreamId == streamId)
            .OrderByDescending(e => e.StreamPosition)
            .Select(e => (int?)e.StreamPosition)
            .Take(1)
            .FirstOrDefaultAsync(ct);
        
        if (pos == null)
            throw new StreamNotFoundException(streamId);
        return (uint)pos.Value;
    }

    public async Task<uint> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(events);

        try
        {
            var r = 0u;
            var list = events.Select(e => _serializer.Serialize(e, streamId, r++)).ToList();
            
            await using var session = WriteSession();
            session.Insert<MartenEvent>(entities: list);
            await session.SaveChangesAsync(cancellationToken);
            return r - 1;
        }
        catch (DocumentAlreadyExistsException ex)
        {
            throw new StreamAlreadyExistsException(streamId, ex);
        }
    }

    public async Task<uint> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(@event);

        try
        {
            const int zero = 0;

            var e = _serializer.Serialize(@event, streamId, zero);
            
            await using var session = WriteSession();
            session.Insert(e);
            await session.SaveChangesAsync(cancellationToken);
            return zero;
        }
        catch (DocumentAlreadyExistsException ex)
        {
            throw new StreamAlreadyExistsException(streamId, ex);
        }
    }

    public async Task<uint> Append(string streamId,
        uint expectedRevision, 
        IEnumerable<UncommittedEvent> events, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(events);

        await using var session = WriteSession();
        var actual = await CheckRevision(streamId, session, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        
        session.Insert(events.Select(e => _serializer.Serialize(e, streamId, ++expectedRevision)));
        await session.SaveChangesAsync(cancellationToken);
        return expectedRevision;
    }

    public async Task<uint> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(events);

        await using var session = WriteSession();
        var r = await CheckRevision(streamId, session, cancellationToken);
        session.Insert(events.Select(e => _serializer.Serialize(e, streamId, ++r)));
        await session.SaveChangesAsync(cancellationToken);
        return r;
    }

    public async Task<uint> Append(string streamId, uint expectedRevision, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(@event);

        await using var session = WriteSession();
        var actual = await CheckRevision(streamId, session, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        session.Insert(_serializer.Serialize(@event, streamId, ++expectedRevision));
        await session.SaveChangesAsync(cancellationToken);
        return expectedRevision;
    }

    public async Task<uint> Append(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(@event);

        await using var session = WriteSession();
        var actual = await CheckRevision(streamId, session, cancellationToken);
        session.Insert(_serializer.Serialize(@event, streamId, ++actual));
        await session.SaveChangesAsync(cancellationToken);
        return actual;
    }

    public async Task SubmitTransaction(IReadOnlyList<UncommittedEventsList> transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Count == 0)
            return;

        await using var session = WriteSession();
        
        var table = _documentStore.GetTableName(typeof(MartenEvent)).QualifiedName;
        var sql =
            $"select data->>'StreamId', max((data->>'StreamPosition')::int) from {table} where data->>'StreamId' = ANY(:ids) group by data->>'StreamId'";
        var current = await session.QueryRaw(sql,
                new Dictionary<string, object?>()
                {
                    {"ids", transaction.Select(k => k.StreamId).ToList()}
                },
                r => new
                {
                    Id = r.GetString(0),
                    Position = (uint)r.GetInt32(1)
                },
                cancellationToken)
            .ToDictionaryAsync(g => g.Id, g => g.Position, cancellationToken);
        var exceptions = new List<Exception>();
        foreach (var (streamId, expectedRevision, events) in transaction)
        {
            if (current.TryGetValue(streamId, out var actual))
            {
                if (expectedRevision == null)
                    exceptions.Add(new StreamAlreadyExistsException(streamId));
                else if (expectedRevision.Value != actual)
                    exceptions.Add(new WrongStreamRevisionException(streamId, expectedRevision.Value, actual));
                else
                    session.Insert(events.Select(e => _serializer.Serialize(e, streamId, ++actual)));
            }
            else
            {
                if (expectedRevision != null)
                {
                    exceptions.Add(new StreamNotFoundException(streamId));
                }
                else
                {
                    uint pos = 0;
                    var records = events.Select(e => _serializer.Serialize(e, streamId, pos++));
                    session.Insert(records);
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
        
        await session.SaveChangesAsync(cancellationToken);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var session = ReadSession();

        IQueryable<MartenEvent> query = session.Query<MartenEvent>();
        
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.Timestamp)
                .ThenBy(e => e.StreamId)
                .ThenBy(e => e.StreamPosition),
            Direction.Backwards => query.OrderByDescending(e => e.Timestamp)
                .ThenByDescending(e => e.StreamId)
                .ThenByDescending(e => e.StreamPosition),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
            yield return _serializer.Deserialize(e);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, 
        uint maxCount, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var session = ReadSession();
        
        IQueryable<MartenEvent> query = session.Query<MartenEvent>();
        
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.Timestamp),
            Direction.Backwards => query.OrderByDescending(e => e.Timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        query = query.Take((int)maxCount);

        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
            yield return _serializer.Deserialize(e);
    }

    private async IAsyncEnumerable<EventEnvelope> ReadInternal(Direction direction,
        string streamId, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var session = ReadSession();
        var query = session.Query<MartenEvent>().Where(e => e.StreamId == streamId);
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.StreamPosition),
            Direction.Backwards => query.OrderByDescending(e => e.StreamPosition),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        var exists = false;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            exists = true;
            yield return _serializer.Deserialize(e);
        }
        if (!exists)
            throw new StreamNotFoundException(streamId);
    }

    public IEventStoreStream Read(Direction direction, string streamId, CancellationToken cancellationToken) =>
        new MartenEventStoreStream(direction, streamId, ReadInternal(direction, streamId, cancellationToken));
    
    //For the below: revision is 0-based, hence the --count

    private async IAsyncEnumerable<EventEnvelope> ReadInternal(Direction direction,
        string streamId, 
        uint expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.StreamPosition);
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.StreamPosition),
            Direction.Backwards => query.OrderByDescending(e => e.StreamPosition),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        var count = uint.MaxValue;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            yield return _serializer.Deserialize(e);
            ++count;
        }

        if (count == uint.MaxValue)
            throw new StreamNotFoundException(streamId);
        if (count != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, count);
    }

    public IEventStoreStream Read(Direction direction, string streamId, uint expectedRevision, CancellationToken cancellationToken) =>
        new MartenEventStoreStream(direction, streamId, ReadInternal(direction, streamId, expectedRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadInternal(
        Direction direction,
        string streamId, 
        uint startRevision, 
        uint expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, expectedRevision);

        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .OrderBy(e => e.StreamPosition)
            .Where(e => e.StreamId == streamId);

        if (startRevision != 0)
            query = query.Where(e => e.StreamPosition >= (int)startRevision || e.StreamPosition == 0);

        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.StreamPosition),
            Direction.Backwards => query.OrderByDescending(e => e.StreamPosition),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        var count = startRevision;
        var found = false;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            found = true;
            if (e.StreamPosition == 0 && startRevision != 0) continue;
            yield return _serializer.Deserialize(e);
            ++count;
        }
        if (!found)
            throw new StreamNotFoundException(streamId);
        --count;
        if (count != expectedRevision)
        {
            var actual = await GetRevision(streamId, cancellationToken);
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        }
    }

    public IEventStoreStream Read(Direction direction, string streamId, uint startRevision, uint expectedRevision, CancellationToken cancellationToken) =>
        new MartenEventStoreStream(direction, streamId, ReadInternal(direction, streamId, startRevision, expectedRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadPartialInternal(
        Direction direction, 
        string streamId, 
        uint count,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfZero(count);

        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .Where(e => e.StreamId == streamId)
            .Take((int)count);
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.StreamPosition),
            Direction.Backwards => query.OrderByDescending(e => e.StreamPosition),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        var read = 0u;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            yield return _serializer.Deserialize(e);
            ++read;
        }

        if (read == 0)
            throw new StreamNotFoundException(streamId);
        if (read != count)
            throw new WrongStreamRevisionException(streamId, count - 1, read - 1);
    }

    public IEventStoreStream ReadPartial(Direction direction, string streamId, uint count, CancellationToken cancellationToken) =>
        new MartenEventStoreStream(direction, streamId, ReadPartialInternal(direction, streamId, count, cancellationToken));
    
    //NB: For reading slice: We always include at position '0'. The reason is to differentiate between 'doesn't exist' and 'read after end'

    private async IAsyncEnumerable<EventEnvelope> ReadSliceInternal(
        string streamId, 
        uint startRevision, 
        uint endRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, endRevision);

        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .Where(e => e.StreamId == streamId)
            .Where(e => e.StreamPosition == 0 
                        || (e.StreamPosition >= (int)startRevision && e.StreamPosition <= (int)endRevision))
            .OrderBy(e => e.StreamPosition);

        var exists = false;
        uint? actual = null;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            exists = true;
            if (e.StreamPosition == 0 && startRevision != 0) continue;
            var env = _serializer.Deserialize(e);
            yield return env;
            actual = env.StreamPosition;
        }
        
        if (!exists)
            throw new StreamNotFoundException(streamId);

        //Stream does exist, ensure we read to the end
        actual ??= await GetRevision(streamId, cancellationToken);
        if (actual.Value != endRevision)
            throw new WrongStreamRevisionException(streamId, endRevision, actual.Value);
    }

    public IEventStoreStream ReadSlice(string streamId, uint startRevision, uint endRevision, CancellationToken cancellationToken) =>
        new MartenEventStoreStream(Direction.Forwards, streamId, ReadSliceInternal(streamId, startRevision, endRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadSliceInternal(string streamId,
        uint startRevision, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .Where(e => e.StreamId == streamId)
            .Where(e => e.StreamPosition == 0 || e.StreamPosition >= (int)startRevision)
            .OrderBy(e => e.StreamPosition);

        var exists = false;
        uint? actual = null;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            exists = true;
            if (e.StreamPosition == 0 && startRevision != 0) continue;
            var env = _serializer.Deserialize(e);
            actual = env.StreamPosition;
            yield return env;
        }

        if (!exists)
            throw new StreamNotFoundException(streamId);

        if (actual.HasValue) yield break;
        
        //Stream exists, but before start
        actual = await GetRevision(streamId, cancellationToken);
        throw new WrongStreamRevisionException(streamId, startRevision, actual.Value);
    }

    public IEventStoreStream ReadSlice(string streamId, uint startRevision, CancellationToken cancellationToken) =>
        new MartenEventStoreStream(Direction.Forwards, streamId, ReadSliceInternal(streamId, startRevision, cancellationToken));

    public async IAsyncEnumerable<IEventStoreStream> ReadMany(Direction direction,
        IEnumerable<string> streams, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var list = streams.Distinct().ToList();
        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .Where(e => list.Contains(e.StreamId));
        query = direction switch
        {
            Direction.Forwards => query
                .OrderBy(e => e.StreamId)
                .ThenBy(e => e.StreamPosition),
            Direction.Backwards => query
                .OrderByDescending(e => e.StreamId)
                .ThenByDescending(e => e.StreamPosition),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        var read = new HashSet<string>();
        var stream = new List<EventEnvelope>();
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            read.Add(e.StreamId);
            if (stream.Count > 0 && e.StreamId != stream[^1].StreamId)
            {
                yield return new MartenEventStoreStream(direction, stream[^1].StreamId, stream.ToAsyncEnumerable());
                stream = [];
            }
            stream.Add(_serializer.Deserialize(e));
        }

        if (stream.Count > 0)
        {
            //Return the last stream
            yield return new MartenEventStoreStream(direction, stream[^1].StreamId, stream.ToAsyncEnumerable());
        }

        // Check for missing streams
        var missing = list.Except(read)
            .Select(id => new StreamNotFoundException(id))
            .ToList();
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

    public async IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, 
        Type[] eventTypes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var types = GetTypeNames(eventTypes);
        await using var session = ReadSession();
        var query = session.Query<MartenEvent>()
            .Where(e => e.EventTypes.Intersects(types));
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.Timestamp),
            Direction.Backwards => query.OrderByDescending(e => e.Timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
            yield return _serializer.Deserialize(e);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction,
        Type[] eventTypes, 
        uint maxCount,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var types = GetTypeNames(eventTypes);
        await using var session = ReadSession();
        var query = session.Query<MartenEvent>()
            .Where(e => e.EventTypes.Intersects(types));
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.Timestamp),
            Direction.Backwards => query.OrderByDescending(e => e.Timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        query = query.Take((int)maxCount);
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
            yield return _serializer.Deserialize(e);
    }

    public async Task<uint> Count(CancellationToken cancellationToken)
    {
        await using var session = ReadSession();
        return (uint) await session.Query<MartenEvent>().LongCountAsync(cancellationToken);
    }

    public async Task<uint> CountByEventType(Type[] eventTypes, CancellationToken cancellationToken)
    {
        var types = GetTypeNames(eventTypes).ToArray();
        await using var session = ReadSession();

        var count = await session.Query<MartenEvent>()
            .Where(e => e.EventTypes.Intersects(types))
            .LongCountAsync(cancellationToken);
        return (uint) count;
    }

    private List<string> GetTypeNames(Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        return eventTypes
            .Distinct()
            .Select(_eventTypeMap.GetTypeNames)
            .Select(l => l[^1])
            .ToList();
    }
}