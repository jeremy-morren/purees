using System.Data;
using System.Data.Common;
using System.Linq.Async;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PureES.EventStore.EFCore.Models;
using PureES.EventStore.EFCore.Providers;
using PureES.EventStore.EFCore.Subscriptions;

namespace PureES.EventStore.EFCore;

internal class EfCoreEventStore<TContext> : IEfCoreEventStore
    where TContext : DbContext
{
    private readonly EventStoreDbContext<TContext> _context;
    private readonly EfCoreEventSerializer _serializer;
    private readonly IEventTypeMap _eventTypeMap;
    private readonly List<IEfCoreEventStoreSubscription> _subscriptions;

    public EfCoreEventStore(
        EventStoreDbContext<TContext> context,
        IEventTypeMap eventTypeMap,
        EfCoreEventSerializer serializer,
        IEnumerable<IHostedService>? hostedServices = null)
    {
        _context = context;
        _serializer = serializer;
        _eventTypeMap = eventTypeMap;
        _subscriptions = hostedServices?.OfType<IEfCoreEventStoreSubscription>().ToList() ?? [];
    }
    
    #region Deserialize
    
    private async IAsyncEnumerable<EventEnvelope> ReadEvents(
        IQueryable<EventStoreEvent> query, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var provider = _context.Provider;
        
        //Only select the columns we need
        var src = 
            from x in query
            select new
            {
                x.StreamId,
                x.StreamPos,
                x.Timestamp,
                x.EventType,
                x.Event,
                x.Metadata
            };

        await _context.Database.OpenConnectionAsync(ct);
        await using var command = src.CreateDbCommand();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var streamId = reader.GetString(0);
            var streamPos = (uint)reader.GetInt32(1);
            var timestamp = provider.ReadTimestamp(reader, 2);
            var eventType = reader.GetString(3);
            var @event = reader.GetString(4);
            var metadata = reader.IsDBNull(5) ? null : reader.GetString(5);
            yield return new EventEnvelope(streamId, 
                streamPos, 
                timestamp, 
                _serializer.DeserializeEvent(streamId, streamPos, eventType, @event),
                _serializer.DeserializeMetadata(streamId, streamPos, metadata));
        }
    }
    
    #endregion

    #region Read
    
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
    
    #endregion
    
    #region Create/Append

    private async Task WriteEvents(IEnumerable<EventStoreEvent> events, CancellationToken ct)
    {
        var result = await _context.Provider.WriteEvents(events, ct);
        foreach (var subscription in _subscriptions)
            subscription.OnEventsWritten(result);
    }

    public async Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(events);

        try
        {
            var r = 0u;
            var list = events.Select(e => _serializer.Serialize(streamId, r++, e)).ToList();
            await WriteEvents(list, cancellationToken);
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
        
        var list = events.Select(e => _serializer.Serialize(streamId, (uint)++actual, e));
        await WriteEvents(list, cancellationToken);
        return actual;
    }

    public async Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var actual = await GetRevision(streamId, cancellationToken);
        var list = events.Select(e => _serializer.Serialize(streamId, (uint)++actual, e));
        await WriteEvents(list, cancellationToken);
        return actual;
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
                else if (actual != stream.ExpectedRevision.Value)
                    exceptions.Add(
                        new WrongStreamRevisionException(streamId, stream.ExpectedRevision.Value, actual));
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
                    var r = 0u;
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
        await WriteEvents(list, cancellationToken);
    }
    
    #endregion
    
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
            .GroupSequentialBy(e => e.StreamId);
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
        var query = FilterByEventType(eventTypes);
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
        if (maxCount.HasValue)
            query = query.Take((int)maxCount.Value);
        
        return ReadEvents(query, cancellationToken);
    }

    private IQueryable<EventStoreEvent> FilterByEventType(Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        var names = eventTypes
            .Select(t => _eventTypeMap.GetTypeNames(t)[^1])
            .Distinct()
            .ToList();
        return _context.QueryEvents()
            .Where(e => e.EventTypes.Any(t => names.Contains(t.TypeName)));
    }
    
    #endregion

    #region Count
    
    public async Task<ulong> Count(CancellationToken cancellationToken)
    {
        return (ulong)await _context.QueryEvents().LongCountAsync(cancellationToken);
    }

    public async Task<ulong> CountByEventType(Type[] eventTypes, CancellationToken cancellationToken)
    {
        var query = FilterByEventType(eventTypes);
        return (ulong)await query.LongCountAsync(cancellationToken);
    }
    
    #endregion

    public string GenerateIdempotentCreateScript()
    {
        var script = _context.Database.GenerateCreateScript();
        return SqlRegexes.ReplaceCreateWithCreateIfNotExists(script);
    }
}