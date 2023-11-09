using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Marten;
using Marten.Exceptions;
using PureES.Core;

namespace PureES.EventStores.Marten;

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
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        
        await using var session = ReadSession();
        return await session.Query<MartenEvent>()
            .Where(e => e.StreamId == streamId && e.StreamPosition == 0)
            .Take(1)
            .AnyAsync(token: cancellationToken);
    }

    public async Task<ulong> GetRevision(string streamId, CancellationToken cancellationToken)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        
        await using var session = ReadSession();
        return await CheckRevision(streamId, session, cancellationToken);
    }

    public async Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken cancellationToken)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        
        var revision = await GetRevision(streamId, cancellationToken);
        if (revision != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, revision);
        return revision;
    }

    private static async Task<ulong> CheckRevision(string streamId, IQuerySession session, CancellationToken ct)
    {
        var pos = await session.Query<MartenEvent>()
            .Where(e => e.StreamId == streamId)
            .OrderByDescending(e => e.StreamPosition)
            .Select(e => (int?)e.StreamPosition)
            .Take(1)
            .FirstOrDefaultAsync(ct);
        
        return (ulong?)pos ?? throw new StreamNotFoundException(streamId);
    }

    public async Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (events == null) throw new ArgumentNullException(nameof(events));

        try
        {
            var r = 0ul;
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

    public async Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
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

    public async Task<ulong> Append(string streamId,
        ulong expectedRevision, 
        IEnumerable<UncommittedEvent> events, 
        CancellationToken cancellationToken)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (events == null) throw new ArgumentNullException(nameof(events));
        
        await using var session = WriteSession();
        var actual = await CheckRevision(streamId, session, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        
        session.Insert(events.Select(e => _serializer.Serialize(e, streamId, ++expectedRevision)));
        await session.SaveChangesAsync(cancellationToken);
        return expectedRevision;
    }

    public async Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (events == null) throw new ArgumentNullException(nameof(events));
        
        await using var session = WriteSession();
        var r = await CheckRevision(streamId, session, cancellationToken);
        session.Insert(events.Select(e => _serializer.Serialize(e, streamId, ++r)));
        await session.SaveChangesAsync(cancellationToken);
        return r;
    }

    public async Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        await using var session = WriteSession();
        var actual = await CheckRevision(streamId, session, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        session.Insert(_serializer.Serialize(@event, streamId, ++expectedRevision));
        await session.SaveChangesAsync(cancellationToken);
        return expectedRevision;
    }

    public async Task<ulong> Append(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        await using var session = WriteSession();
        var actual = await CheckRevision(streamId, session, cancellationToken);
        session.Insert(_serializer.Serialize(@event, streamId, ++actual));
        await session.SaveChangesAsync(cancellationToken);
        return actual;
    }

    public async IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var session = ReadSession();

        IQueryable<MartenEvent> query = session.Query<MartenEvent>();
        
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.Timestamp),
            Direction.Backwards => query.OrderByDescending(e => e.Timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
            yield return _serializer.Deserialize(e);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, 
        ulong maxCount, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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

    public async IAsyncEnumerable<EventEnvelope> Read(Direction direction, 
        string streamId, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    
    //For the below: revision is 0-based, hence the --count

    public async IAsyncEnumerable<EventEnvelope> Read(Direction direction, 
        string streamId, 
        ulong expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .Where(e => e.StreamId == streamId);
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.StreamPosition),
            Direction.Backwards => query.OrderByDescending(e => e.StreamPosition),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        var count = ulong.MaxValue;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            yield return _serializer.Deserialize(e);
            ++count;
        }

        if (count == ulong.MaxValue)
            throw new StreamNotFoundException(streamId);
        if (count != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, count);
    }

    public async IAsyncEnumerable<EventEnvelope> Read(Direction direction, 
        string streamId, 
        ulong startRevision, 
        ulong expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));

        if (startRevision > expectedRevision)
            throw new ArgumentOutOfRangeException(nameof(startRevision));
        
        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .Where(e => e.StreamId == streamId)
            .Where(e => e.StreamPosition >= (int)startRevision);
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.StreamPosition),
            Direction.Backwards => query.OrderByDescending(e => e.StreamPosition),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        var count = startRevision;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            yield return _serializer.Deserialize(e);
            ++count;
        }
        if (count == startRevision)
            throw new StreamNotFoundException(streamId);
        --count;
        if (count != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, count);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadPartial(Direction direction, 
        string streamId, 
        ulong count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        
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
        var read = 0ul;
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

    public async IAsyncEnumerable<EventEnvelope> ReadSlice(string streamId, 
        ulong startRevision, 
        ulong endRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));

        if (startRevision > endRevision)
            throw new ArgumentOutOfRangeException(nameof(startRevision));
        
        await using var session = ReadSession();
        var query = session
            .Query<MartenEvent>()
            .Where(e => e.StreamId == streamId)
            .Where(e => e.StreamPosition >= (int)startRevision)
            .Where(e => e.StreamPosition <= (int)endRevision);
        
        var revision = startRevision;
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            yield return _serializer.Deserialize(e);
            ++revision;
        }

        if (revision == startRevision)
            throw new StreamNotFoundException(streamId);
        --revision;
        if (revision != endRevision)
            throw new WrongStreamRevisionException(streamId, endRevision, revision);
    }

    public async IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction,
        IEnumerable<string> streams, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = streams.ToList();
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
        var stream = new List<EventEnvelope>();
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            if (stream.Count > 0 && e.StreamId != stream[^1].StreamId)
            {
                yield return stream.ToAsyncEnumerable();
                stream = new List<EventEnvelope>();
            }

            stream.Add(_serializer.Deserialize(e));
        }

        if (stream.Count > 0)
            yield return stream.ToAsyncEnumerable();
    }

    public async IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, 
        IAsyncEnumerable<string> streams, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = await streams.ToListAsync(cancellationToken);
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
        var stream = new List<EventEnvelope>();
        await foreach (var e in query.ToAsyncEnumerable(cancellationToken))
        {
            if (stream.Count > 0 && e.StreamId != stream[^1].StreamId)
            {
                yield return stream.ToAsyncEnumerable();
                stream = new List<EventEnvelope>();
            }

            stream.Add(_serializer.Deserialize(e));
        }

        if (stream.Count > 0)
            yield return stream.ToAsyncEnumerable();
    }

    public async IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, 
        Type eventType,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var type = _eventTypeMap.GetTypeName(eventType);
        await using var session = ReadSession();
        var query = session.Query<MartenEvent>().Where(e => e.EventType == type);
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
        Type eventType, 
        ulong maxCount,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var type = _eventTypeMap.GetTypeName(eventType);
        await using var session = ReadSession();
        var query = session.Query<MartenEvent>().Where(e => e.EventType == type);
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

    public async Task<ulong> Count(CancellationToken cancellationToken)
    {
        await using var session = ReadSession();
        return (ulong) await session.Query<MartenEvent>().LongCountAsync(cancellationToken);
    }

    public async Task<ulong> CountByEventType(Type eventType, CancellationToken cancellationToken)
    {
        var type = _eventTypeMap.GetTypeName(eventType);
        await using var session = ReadSession();
        return (ulong) await session.Query<MartenEvent>()
            .Where(e => e.EventType == type)
            .LongCountAsync(cancellationToken);
    }
}