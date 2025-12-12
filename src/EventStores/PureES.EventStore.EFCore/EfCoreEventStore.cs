using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PureES.EventStore.EFCore.Models;
using PureES.EventStore.EFCore.Subscriptions;

namespace PureES.EventStore.EFCore;

/// <summary>
/// The EFCore event store implementation
/// </summary>
/// <typeparam name="TContext">The context we are copying options from</typeparam>
internal class EfCoreEventStore<TContext> : IEfCoreEventStore where TContext : DbContext
{
    private readonly IDbContextFactory<EventStoreDbContext<TContext>> _contextFactory;
    private readonly EfCoreEventSerializer _serializer;
    private readonly IEventTypeMap _eventTypeMap;
    private readonly List<IEfCoreEventStoreSubscription> _subscriptions;

    public EfCoreEventStore(
        IDbContextFactory<EventStoreDbContext<TContext>> contextFactory,
        IEventTypeMap eventTypeMap,
        EfCoreEventSerializer serializer,
        IEnumerable<IHostedService>? hostedServices = null)
    {
        _contextFactory = contextFactory;
        _serializer = serializer;
        _eventTypeMap = eventTypeMap;
        _subscriptions = hostedServices?.OfType<IEfCoreEventStoreSubscription>().ToList() ?? [];
    }
    
    #region Deserialize
    
    private async IAsyncEnumerable<EventEnvelope> ReadEvents(
        Func<EventStoreDbContext<TContext>, IQueryable<EventStoreEvent>> createQuery,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var provider = context.Provider;
        var query = createQuery(context);

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

        await using var command = src.CreateDbCommand();
        await using var _ = await OpenConnectionWrapper.OpenAsync(command, ct);
        await command.PrepareAsync(ct); //This is a frequent operation, prepare the command
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var streamId = reader.GetString(0);
            var streamPos = reader.GetInt32(1);
            var timestamp = provider.ReadTimestamp(reader, 2);
            var eventType = reader.GetString(3);
            var @event = reader.GetString(4);
            var metadata = reader.IsDBNull(5) ? null : reader.GetString(5);

            yield return CreateEnvelope(streamId, streamPos, timestamp, eventType, @event, metadata, _serializer);
        }

        yield break;

        static EventEnvelope CreateEnvelope(
            string streamId,
            int streamPos,
            DateTime timestamp,
            string eventType,
            string @event,
            string? metadata,
            EfCoreEventSerializer serializer)
        {
            return new EventEnvelope(
                streamId,
                (uint)streamPos,
                timestamp,
                serializer.DeserializeEvent(streamId, streamPos, eventType, @event),
                serializer.DeserializeMetadata(streamId, streamPos, metadata));
        }
    }
    
    #endregion

    #region Read
    
    public async Task<bool> Exists(string streamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.QueryEvents()
            .AnyAsync(e => e.StreamId == streamId, cancellationToken);
    }


    private static async Task<int> GetRevisionInternal(EventStoreDbContext<TContext> context, string streamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var pos = await context.QueryEvents()
            .Where(e => e.StreamId == streamId)
            .OrderByDescending(e => e.StreamPos)
            .Select(e => (int?)e.StreamPos)
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (pos == null)
            throw new StreamNotFoundException(streamId);
        return pos.Value;
    }

    public async Task<uint> GetRevision(string streamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return (uint)await GetRevisionInternal(context, streamId, cancellationToken);
    }

    public async Task<uint> GetRevision(string streamId, uint expectedRevision, CancellationToken cancellationToken)
    {
        var actual = await GetRevision(streamId, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        return actual;
    }
    
    #endregion
    
    #region Create/Append

    private async Task WriteEvents(EventStoreDbContext<TContext> context, IEnumerable<EventStoreEvent> events, CancellationToken ct)
    {
        var result = await context.Provider.WriteEvents(events, ct);
        foreach (var subscription in _subscriptions)
            subscription.OnEventsWritten(result);
    }

    public async Task<uint> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(events);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            var r = 0;
            var list = events.Select(e => _serializer.Serialize(streamId, r++, e)).ToList();
            await WriteEvents(context, list, cancellationToken);
            return (uint)list.Count - 1;
        }
        catch (DbUpdateException ex) when 
            (ex.InnerException is DbException dex && context.Provider.IsUniqueConstraintFailedException(dex))
        {
            throw new StreamAlreadyExistsException(streamId, ex);
        }
    }

    public Task<uint> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        return Create(streamId, [@event], cancellationToken);
    }

    public async Task<uint> Append(string streamId, uint expectedRevision, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expectedRevision, (uint)int.MaxValue);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var actual = await GetRevisionInternal(context, streamId, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, (uint)actual);
        
        var list = events.Select(e => _serializer.Serialize(streamId, ++actual, e));
        await WriteEvents(context, list, cancellationToken);
        return (uint)actual;
    }

    public async Task<uint> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var actual = await GetRevisionInternal(context, streamId, cancellationToken);
        var list = events.Select(e => _serializer.Serialize(streamId, ++actual, e));
        await WriteEvents(context, list, cancellationToken);
        return (uint)actual;
    }

    public Task<uint> Append(string streamId, uint expectedRevision, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expectedRevision, (uint)int.MaxValue);
        return Append(streamId, expectedRevision, [@event], cancellationToken);
    }

    public Task<uint> Append(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return Append(streamId, [@event], cancellationToken);
    }

    public async Task SubmitTransaction(IReadOnlyList<UncommittedEventsList> transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Count == 0)
            return; //Empty transaction

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        //Get actual revisions for all streams
        var streamIds = transaction.Select(x => x.StreamId);
        var revisions = await context.QueryEvents()
            .Where(e => streamIds.Contains(e.StreamId))
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
        foreach (var (streamId, expectedRevision, events) in transaction)
        {
            if (revisions.TryGetValue(streamId, out var actual))
            {
                //Stream exists, check revision matches
                if (expectedRevision == null)
                    exceptions.Add(new StreamAlreadyExistsException(streamId));
                else if (actual != expectedRevision.Value)
                    exceptions.Add(
                        new WrongStreamRevisionException(streamId, expectedRevision.Value, (uint)actual));
                else
                    //Stream exists and revision matches, append events
                    list.AddRange(events.Select(x => _serializer.Serialize(streamId, ++actual, x)));
            }
            else
            {
                //Stream does not exist, check revision matches
                if (expectedRevision != null)
                {
                    exceptions.Add(new StreamNotFoundException(streamId));
                }
                else
                {
                    //Stream does not exist and revision matches, create stream
                    var r = 0;
                    list.AddRange(events.Select(x => _serializer.Serialize(streamId, r++, x)));
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
        await WriteEvents(context, list, cancellationToken);
    }
    
    #endregion
    
    #region Read All
    
    private IQueryable<EventStoreEvent> ReadAll(EventStoreDbContext<TContext> context, Direction direction)
    {
        var query = context.QueryEvents();
        return direction switch
        {
            Direction.Forwards => query
                .OrderBy(e => e.Timestamp)
                .ThenBy(e => e.StreamId)
                .ThenBy(e => e.StreamPos),
            Direction.Backwards => query
                .OrderByDescending(e => e.Timestamp)
                .ThenByDescending(e => e.StreamId)
                .ThenByDescending(e => e.StreamPos),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, CancellationToken cancellationToken) =>
        ReadEvents(context => ReadAll(context, direction), cancellationToken);

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, uint maxCount, CancellationToken cancellationToken) =>
        ReadEvents(
            context => ReadAll(context, direction).Take((int)maxCount),
            cancellationToken);

    #endregion
    
    #region Read Stream

    private static IQueryable<EventStoreEvent> ReadStream(EventStoreDbContext<TContext> context, Direction direction, string streamId)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        var query = context.QueryEvents().Where(e => e.StreamId == streamId);

        return direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.StreamPos),
            Direction.Backwards => query.OrderByDescending(e => e.StreamPos),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }
    
    private async IAsyncEnumerable<EventEnvelope> ReadInternal(Direction direction, string streamId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var events = ReadEvents(
            context => ReadStream(context, direction, streamId),
            cancellationToken);
        
        var exists = false;
        await foreach (var e in events)
        {
            exists = true;
            yield return e;
        }
        if (!exists)
            throw new StreamNotFoundException(streamId);
    }

    public IEventStoreStream Read(Direction direction, string streamId, CancellationToken cancellationToken) =>
        new EfCoreEventStoreStream(direction, streamId, ReadInternal(direction, streamId, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadInternal(
        Direction direction, 
        string streamId, 
        uint expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expectedRevision, (uint)int.MaxValue);

        var events = ReadEvents(
            context => ReadStream(context, direction, streamId),
            cancellationToken);
        
        var actual = uint.MaxValue;
        await foreach (var e in events)
        {
            yield return e;
            ++actual; //First event will wrap to 0
        }

        if (actual == uint.MaxValue)
            throw new StreamNotFoundException(streamId);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
    }

    public IEventStoreStream Read(Direction direction, string streamId, uint expectedRevision, CancellationToken cancellationToken) =>
        new EfCoreEventStoreStream(direction, streamId, ReadInternal(direction, streamId, expectedRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadInternal(
        Direction direction, 
        string streamId,
        uint startRevision, 
        uint expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, (uint)int.MaxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expectedRevision, (uint)int.MaxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, expectedRevision);

        var events = ReadEvents(
            context =>
            {
                var query = ReadStream(context, direction, streamId);
                // See comment in ReadSlice(string, uint, uint, CancellationToken)
                if (startRevision != 0)
                    query = query.Where(e => e.StreamPos == 0 || e.StreamPos >= (int)startRevision);
                return query;
            },
            cancellationToken);

        var actual = startRevision;
        var exists = false;
        await foreach (var e in events)
        {
            exists = true;
            if (e.StreamPosition == 0 && startRevision != 0) continue;
            yield return e;
            ++actual;
        }
        if (!exists)
            throw new StreamNotFoundException(streamId);

        if (actual == startRevision)
        {
            // Nothing read, stream exists but before startRevision
            // Get actual revision to throw correct exception
            actual = await GetRevision(streamId, cancellationToken);
            Debug.Assert(actual != startRevision);
        }
        else
        {
            // Stream does exist
            --actual;
        }
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
    }

    public IEventStoreStream Read(Direction direction, string streamId, uint startRevision, uint expectedRevision, CancellationToken cancellationToken) =>
        new EfCoreEventStoreStream(direction, streamId, ReadInternal(direction, streamId, startRevision, expectedRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadPartialInternal(
        Direction direction, 
        string streamId, 
        uint count,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (uint)int.MaxValue);

        var events = ReadEvents(
            context => ReadStream(context, direction, streamId).Take((int)count),
            cancellationToken);
        var read = 0u;
        await foreach (var e in events)
        {
            yield return e;
            ++read;
        }

        if (read == 0)
            throw new StreamNotFoundException(streamId);
        if (read != count)
            throw new WrongStreamRevisionException(streamId, count - 1, read - 1);
    }

    public IEventStoreStream ReadPartial(Direction direction, string streamId, uint count, CancellationToken cancellationToken) =>
        new EfCoreEventStoreStream(direction, streamId, ReadPartialInternal(direction, streamId, count, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadSliceInternal(
        string streamId, 
        uint startRevision, 
        uint endRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, (uint)int.MaxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(endRevision, (uint)int.MaxValue);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, endRevision);

        var events = ReadEvents(
            context =>
            {
                var query = ReadStream(context, Direction.Forwards, streamId);

                // Ensure we read the first event, to check stream exists
                // If startRevision is 0, no filter is needed (i.e. we read up to endRevision)
                if (startRevision != 0)
                    query = query.Where(e => e.StreamPos == 0 || e.StreamPos >= (int)startRevision);

                return query.Where(e => e.StreamPos <= (int)endRevision);
            },
            cancellationToken);
        
        var exists = false;
        uint? actual = null;
        await foreach (var e in events)
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

    public IEventStoreStream ReadSlice(string streamId, uint startRevision, uint endRevision, CancellationToken cancellationToken) =>
        new EfCoreEventStoreStream(Direction.Forwards, streamId, ReadSliceInternal(streamId, startRevision, endRevision, cancellationToken));

    private async IAsyncEnumerable<EventEnvelope> ReadSliceInternal(
        string streamId, 
        uint startRevision, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, (uint)int.MaxValue);

        var events = ReadEvents(
            context =>
            {
                var query = ReadStream(context, Direction.Forwards, streamId);

                //See comment in ReadSlice(string, uint, uint, CancellationToken)
                if (startRevision != 0)
                    query = query.Where(e => e.StreamPos == 0 || e.StreamPos >= (int)startRevision);

                return query;
            },
            cancellationToken);
        
        var exists = false;
        uint? actual = null;
        await foreach (var e in events)
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

    public IEventStoreStream ReadSlice(string streamId, uint startRevision, CancellationToken cancellationToken) =>
        new EfCoreEventStoreStream(Direction.Forwards, streamId, ReadSliceInternal(streamId, startRevision, cancellationToken));
    
    #endregion
    
    #region Read Many

    public async IAsyncEnumerable<IEventStoreStream> ReadMany(
        Direction direction, 
        IEnumerable<string> streams, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streams);

        var list = streams.ToList();

        var source = ReadEvents(
                context =>
                {
                    var query = context.QueryEvents()
                        .Where(e => list.Contains(e.StreamId));

                    return direction switch
                    {
                        Direction.Forwards => query
                            .OrderBy(e => e.StreamId)
                            .ThenBy(e => e.StreamPos),

                        Direction.Backwards => query
                            .OrderByDescending(e => e.StreamId)
                            .ThenByDescending(e => e.StreamPos),
                        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
                    };
                },
                cancellationToken)
            .GroupSequentialBy(e => e.StreamId);

        var read = new HashSet<string>();
        await foreach (var group in source)
        {
            read.Add(group.Key);
            yield return new EfCoreEventStoreStream(direction, group.Key, group);
        }

        // Check if all streams were read
        var exceptions = list.Except(read)
            .Select(streamId => new StreamNotFoundException(streamId))
            .ToList();
        switch (exceptions.Count)
        {
            case 0:
                yield break;
            case 1:
                throw exceptions[0];
            default:
                throw new AggregateException(exceptions);
        }
    }

    public async IAsyncEnumerable<IEventStoreStream> ReadMany(
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
        uint maxCount,
        CancellationToken cancellationToken)
    {
        return ReadByEventTypeInternal(direction, eventTypes, maxCount, cancellationToken);
    }
    
    private IAsyncEnumerable<EventEnvelope> ReadByEventTypeInternal(
        Direction direction, 
        Type[] eventTypes, 
        uint? maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        if (eventTypes.Length == 0)
            return AsyncEnumerable.Empty<EventEnvelope>(); //No events to read

        return ReadEvents(
            context =>
            {
                var query = FilterByEventType(context, eventTypes);
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

                return query;
            },
            cancellationToken);
    }

    private IQueryable<EventStoreEvent> FilterByEventType(EventStoreDbContext<TContext> context, Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        var names = eventTypes
            .Select(t => _eventTypeMap.GetTypeNames(t)[^1])
            .Distinct()
            .ToList();
        return context.QueryEvents()
            .Where(e => e.EventTypes.Any(t => names.Contains(t.TypeName)));
    }
    
    #endregion

    #region Count
    
    public async Task<uint> Count(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var count = await context.QueryEvents().LongCountAsync(cancellationToken);
        return (uint)count;
    }

    public async Task<uint> CountByEventType(Type[] eventTypes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var count = await FilterByEventType(context, eventTypes).LongCountAsync(cancellationToken);
        return (uint)count;
    }
    
    #endregion

    public string GenerateIdempotentCreateScript()
    {
        using var context = _contextFactory.CreateDbContext();
        var script = context.Database.GenerateCreateScript();
        return SqlRegexes.ReplaceCreateWithCreateIfNotExists(script);
    }
}