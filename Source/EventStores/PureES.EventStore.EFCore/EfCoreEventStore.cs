using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PureES.EventStore.EFCore;

internal class EfCoreEventStore<TContext> : IEventStore
    where TContext : DbContext
{
    private readonly EventStoreDbContext<TContext> _context;
    private readonly EfCoreEventSerializer _serializer;

    public EfCoreEventStore(EventStoreDbContext<TContext> context, EfCoreEventSerializer serializer)
    {
        _context = context;
        _serializer = serializer;
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
        catch (DbException ex) when (_context.Provider.IsAlreadyExistsException(ex))
        {
            throw new StreamAlreadyExistsException(streamId, ex);
        }
    }

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        return Create(streamId, [@event], cancellationToken);
    }

    public Task<ulong> Append(string streamId, ulong expectedRevision, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ulong> Append(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SubmitTransaction(IReadOnlyDictionary<string, UncommittedEventsList> transaction, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, CancellationToken cancellationToken = default)
    {
        var query = _context.QueryEvents();
        query = direction switch
        {
            Direction.Forwards => query.OrderBy(e => e.Timestamp),
            Direction.Backwards => query.OrderByDescending(e => e.Timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        return _context.ReadEvents(query, _serializer, cancellationToken);
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, ulong maxCount, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

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
    
    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        
        var query = ReadStream(direction, streamId);
        return _context.ReadEvents(query, _serializer, cancellationToken);
    }

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, ulong expectedRevision,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, ulong startRevision, ulong expectedRevision,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> ReadPartial(Direction direction, string streamId, ulong count,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> ReadSlice(string streamId, ulong startRevision, ulong endRevision,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> ReadSlice(string streamId, ulong startRevision, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, IEnumerable<string> streams, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, IAsyncEnumerable<string> streams, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, Type[] eventTypes, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, Type[] eventTypes, ulong maxCount,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<ulong> Count(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ulong> CountByEventType(Type[] eventTypes, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}