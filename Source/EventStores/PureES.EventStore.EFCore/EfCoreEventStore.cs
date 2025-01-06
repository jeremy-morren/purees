using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.EntityFrameworkCore;

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

    public Task SubmitTransaction(IReadOnlyDictionary<string, UncommittedEventsList> transaction, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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
        return _context.ReadEvents(query, _serializer, cancellationToken);
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, ulong maxCount, CancellationToken cancellationToken)
    {
        var query = ReadAll(direction).Take((int)maxCount);
        return _context.ReadEvents(query, _serializer, cancellationToken);
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
    
    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        var query = ReadStream(direction, streamId);
        return _context.ReadEvents(query, _serializer, cancellationToken);
    }

    public async IAsyncEnumerable<EventEnvelope> Read(
        Direction direction, 
        string streamId, 
        ulong expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        var query = _context.ReadEvents(ReadStream(direction, streamId), _serializer, cancellationToken);
        var actual = ulong.MaxValue;
        await foreach (var e in query)
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
        
        var enumerable = _context.ReadEvents(query, _serializer, cancellationToken);
        var count = startRevision;
        await foreach (var e in enumerable)
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
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        
        var query = ReadStream(direction, streamId).Take((int)count);
        var enumerable = _context.ReadEvents(query, _serializer, cancellationToken);
        var read = 0ul;
        await foreach (var e in enumerable)
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
        var enumerable = _context.ReadEvents(query, _serializer, cancellationToken);
        
        var exists = false;
        ulong? actual = null;
        await foreach (var e in enumerable)
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
        var enumerable = _context.ReadEvents(query, _serializer, cancellationToken);
        
        var exists = false;
        ulong? actual = null;
        await foreach (var e in enumerable)
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

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, IEnumerable<string> streams, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, IAsyncEnumerable<string> streams, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, Type[] eventTypes, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, Type[] eventTypes, ulong maxCount,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static Task<ulong> Count(IQueryable<EventStoreEvent> queryable, CancellationToken ct)
    {
        return queryable.LongCountAsync(ct)
            .ContinueWith(t => (ulong)t.Result, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    public Task<ulong> Count(CancellationToken cancellationToken)
    {
        return Count(_context.QueryEvents(), cancellationToken);
    }

    public Task<ulong> CountByEventType(Type[] eventTypes, CancellationToken cancellationToken)
    {
        var types = eventTypes.SelectMany(_eventTypeMap.GetTypeNames).Distinct().ToList();
        var query = _context.QueryEvents().Where(e => e.EventTypes.Any(t => types.Contains(t)));
        return Count(query, cancellationToken);
    }
}