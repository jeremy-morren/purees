using System.Data.Common;
using System.Linq.Async;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore;

internal class EfCoreEventStore<TContext> : IEventStore
    where TContext : DbContext
{
    private readonly EventStoreDbContext<TContext> _context;
    private readonly EfCoreEventSerializer _serializer;
    private readonly IEventTypeMap _eventTypeMap;

    public EfCoreEventStore(EventStoreDbContext<TContext> context, 
        EfCoreEventSerializer serializer,
        IEventTypeMap eventTypeMap)
    {
        _context = context;
        _serializer = serializer;
        _eventTypeMap = eventTypeMap;
    }
    
    #region Provider
    
    private IAsyncEnumerable<EventEnvelope> ReadEvents(IQueryable<EventStoreEvent> queryable, CancellationToken ct)
    {
        return _context.Provider.ReadEvents(queryable, _serializer, ct);
    }
    
    private IAsyncEnumerable<EventEnvelope> ReadEvents(DbCommand command, CancellationToken ct)
    {
        return _context.Provider.ReadEvents(command, _serializer, ct);
    }
    
    #endregion

    public Task<bool> Exists(string streamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        return _context.QueryEvents().AnyAsync(e => e.StreamId == streamId, cancellationToken);
    }

    public async Task<ulong> GetRevision(string streamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        var pos = await _context.QueryEvents()
            .Where(e => e.StreamId == streamId)
            .OrderByDescending(e => e.StreamPos)
            .Select(e => (int?)e.StreamPos)
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (pos == null)
            throw new StreamNotFoundException(streamId);
        return (ulong)pos.Value;
    }

    public async Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken cancellationToken)
    {
        var actual = await GetRevision(streamId, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        return actual;
    }

    public async Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(events);

        try
        {
            var r = 0;
            var list = events.Select(e => _serializer.Serialize(streamId, r++, e)).ToList();
            await _context.WriteEvents(list, cancellationToken);
            return (ulong)list.Count - 1;
        }
        catch (DbUpdateException ex) when 
            (ex.InnerException is DbException dex && _context.Provider.IsUniqueConstraintFailedException(dex))
        {
            throw new StreamAlreadyExistsException(streamId, ex);
        }
    }

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        return Create(streamId, [@event], cancellationToken);
    }

    public async Task<ulong> Append(string streamId, ulong expectedRevision, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var actual = await GetRevision(streamId, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        
        var r = (int)expectedRevision;
        var list = events.Select(e => _serializer.Serialize(streamId, ++r, e));
        await _context.WriteEvents(list, cancellationToken);
        return (ulong)r;
    }

    public async Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var actual = await GetRevision(streamId, cancellationToken);
        var r = (int)actual;
        var list = events.Select(e => _serializer.Serialize(streamId, ++r, e));
        await _context.WriteEvents(list, cancellationToken);
        return (ulong)r;
    }

    public Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        return Append(streamId, expectedRevision, [@event], cancellationToken);
    }

    public Task<ulong> Append(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        return Append(streamId, [@event], cancellationToken);
    }

    public async Task SubmitTransaction(IReadOnlyDictionary<string, UncommittedEventsList> transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        
        //Get actual revisions for all streams
        var revisions = await _context.QueryEvents()
            .Where(e => transaction.Keys.Contains(e.StreamId))
            .GroupBy(e => e.StreamId)
            .Select(g => new
            {
                StreamId = g.Key,
                Revision = g.Max(e => e.StreamPos)
            })
            .ToDictionaryAsync(g => g.StreamId, g => g.Revision, cancellationToken);
        
        //Check if all streams are at the expected revision
        var list = new List<EventStoreEvent>();
        var exceptions = new List<Exception>();
        foreach (var (streamId, stream) in transaction)
        {
            if (revisions.TryGetValue(streamId, out var actual))
            {
                //Stream exists, check revision matches
                if (stream.ExpectedRevision == null)
                    exceptions.Add(new StreamAlreadyExistsException(streamId));
                else if ((ulong)actual != stream.ExpectedRevision.Value)
                    exceptions.Add(
                        new WrongStreamRevisionException(streamId, stream.ExpectedRevision.Value, (ulong)actual));
                else
                    //Stream exists and revision matches, append events
                    list.AddRange(stream.Events.Select(x => _serializer.Serialize(streamId, ++actual, x)));
            }
            else
            {
                //Stream does not exist, check revision matches
                if (stream.ExpectedRevision != null)
                {
                    exceptions.Add(new StreamNotFoundException(streamId));
                }
                else
                {
                    //Stream does not exist and revision matches, create stream
                    var r = 0;
                    list.AddRange(stream.Events.Select(x => _serializer.Serialize(streamId, r++, x)));
                }
            }
        }

        switch (exceptions.Count)
        {
            case 1:
                throw exceptions[0];
            case > 1:
                throw new EventsTransactionException(exceptions);
        }
        
        //No exceptions, write events
        await _context.WriteEvents(list, cancellationToken);
    }
    
    #region Read All
    
    private IQueryable<EventStoreEvent> ReadAll(Direction direction)
    {
        var query = _context.QueryEvents();
        return direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.Timestamp),
            Direction.Backwards => query.OrderByDescending(e => e.Timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, CancellationToken cancellationToken)
    {
        var query = ReadAll(direction);
        return ReadEvents(query, cancellationToken);
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, ulong maxCount, CancellationToken cancellationToken)
    {
        var query = ReadAll(direction).Take((int)maxCount);
        return ReadEvents(query, cancellationToken);
    }
    
    #endregion
    
    #region Read Stream

    private IQueryable<EventStoreEvent> ReadStream(Direction direction, string streamId)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        var query = _context.QueryEvents().Where(e => e.StreamId == streamId);

        return direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.StreamPos),
            Direction.Backwards => query.OrderByDescending(e => e.StreamPos),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }
    
    public async IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        var query = ReadStream(direction, streamId);
        
        var exists = false;
        await foreach (var e in ReadEvents(query, cancellationToken))
        {
            exists = true;
            yield return e;
        }
        if (!exists)
            throw new StreamNotFoundException(streamId);
    }

    public async IAsyncEnumerable<EventEnvelope> Read(
        Direction direction, 
        string streamId, 
        ulong expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        var query = ReadStream(direction, streamId);
        
        var actual = ulong.MaxValue;
        await foreach (var e in ReadEvents(query, cancellationToken))
        {
            yield return e;
            ++actual;
        }

        if (actual == ulong.MaxValue)
            throw new StreamNotFoundException(streamId);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
    }

    public async IAsyncEnumerable<EventEnvelope> Read(
        Direction direction, 
        string streamId,
        ulong startRevision, 
        ulong expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, expectedRevision);
        
        var query = ReadStream(direction, streamId)
            .Where(e => e.StreamPos >= (int)startRevision);
        
        var count = startRevision;
        await foreach (var e in ReadEvents(query, cancellationToken))
        {
            yield return e;
            ++count;
        }
        
        if (count == startRevision)
            throw new StreamNotFoundException(streamId);
        --count;
        if (count != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, count);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadPartial(
        Direction direction, 
        string streamId, 
        ulong count,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        
        var query = ReadStream(direction, streamId).Take((int)count);
        var read = 0ul;
        await foreach (var e in ReadEvents(query, cancellationToken))
        {
            yield return e;
            ++read;
        }

        if (read == 0)
            throw new StreamNotFoundException(streamId);
        if (read != count)
            throw new WrongStreamRevisionException(streamId, count - 1, read - 1);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadSlice(
        string streamId, 
        ulong startRevision, 
        ulong endRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, endRevision);

        var query = ReadStream(Direction.Forwards, streamId)
            .Where(e => e.StreamPos == 0 || (e.StreamPos >= (int)startRevision && e.StreamPos <= (int)endRevision));
        
        var exists = false;
        ulong? actual = null;
        await foreach (var e in ReadEvents(query, cancellationToken))
        {
            exists = true;
            if (e.StreamPosition == 0 && startRevision != 0) continue;
            yield return e;
            actual = e.StreamPosition;
        }
        
        if (!exists)
            throw new StreamNotFoundException(streamId);

        //Stream does exist, ensure we read to the end
        actual ??= await GetRevision(streamId, cancellationToken);
        if (actual.Value != endRevision)
            throw new WrongStreamRevisionException(streamId, endRevision, actual.Value);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadSlice(
        string streamId, 
        ulong startRevision, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var query = ReadStream(Direction.Forwards, streamId)
            .Where(e => e.StreamPos == 0 || e.StreamPos >= (int)startRevision);
        
        var exists = false;
        ulong? actual = null;
        await foreach (var e in ReadEvents(query, cancellationToken))
        {
            exists = true;
            if (e.StreamPosition == 0 && startRevision != 0) continue;
            yield return e;
            actual = e.StreamPosition;
        }
        
        if (!exists)
            throw new StreamNotFoundException(streamId);

        if (actual.HasValue) yield break;
        
        //Stream exists, but before start
        actual = await GetRevision(streamId, cancellationToken);
        throw new WrongStreamRevisionException(streamId, startRevision, actual.Value);
    }
    
    #endregion
    
    #region Read Many

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(
        Direction direction, 
        IEnumerable<string> streams, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streams);

        var query = _context.QueryEvents()
            .Where(e => streams.Contains(e.StreamId));
        
        query = direction switch
        {
            Direction.Forwards => query
                .OrderBy(e => e.StreamId)
                .ThenBy(e => e.StreamPos),

            Direction.Backwards => query
                .OrderByDescending(e => e.StreamId)
                .ThenByDescending(e => e.StreamPos),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        return ReadEvents(query, cancellationToken)
            .GroupSequential(e => e.StreamId);
    }

    public async IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(
        Direction direction, 
        IAsyncEnumerable<string> streams, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var list = await streams.ToListAsync(cancellationToken);
        await foreach (var e in ReadMany(direction, list, cancellationToken))
            yield return e;
    }
    
    #endregion
    
    #region Read by event type

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(
        Direction direction, 
        Type[] eventTypes, 
        CancellationToken cancellationToken)
    {
        return ReadByEventTypeInternal(direction, eventTypes, null, cancellationToken);
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(
        Direction direction, 
        Type[] eventTypes, 
        ulong maxCount,
        CancellationToken cancellationToken)
    {
        return ReadByEventTypeInternal(direction, eventTypes, maxCount, cancellationToken);
    }
    
    private IAsyncEnumerable<EventEnvelope> ReadByEventTypeInternal(
        Direction direction, 
        Type[] eventTypes, 
        ulong? maxCount,
        CancellationToken cancellationToken)
    {
        var query = _context.QueryEvents();
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.Timestamp)
                .ThenBy(e => e.StreamId)
                .ThenBy(e => e.StreamPos),
            Direction.Backwards => query.OrderByDescending(e => e.Timestamp)
                .ThenByDescending(e => e.StreamId)
                .ThenByDescending(e => e.StreamPos),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        
        var command = _context.Provider.FilterByEventType(query, GetEventTypeNames(eventTypes), maxCount);
        return ReadEvents(command, cancellationToken);
    }
    
    #endregion

    public async Task<ulong> Count(CancellationToken cancellationToken)
    {
        return (ulong)await _context.QueryEvents().LongCountAsync(cancellationToken);
    }

    public async Task<ulong> CountByEventType(Type[] eventTypes, CancellationToken cancellationToken)
    {
        var query = _context.Provider.CountByEventType(GetEventTypeNames(eventTypes));
        return (ulong)await query.SingleAsync(cancellationToken);
    }
    
    private List<string> GetEventTypeNames(Type[] eventTypes) =>
        eventTypes
            .Select(t => _eventTypeMap.GetTypeNames(t)[^1])
            .Distinct()
            .ToList();
}