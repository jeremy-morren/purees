using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Internal;
using PureES.EventStore.InMemory.Subscription;

// ReSharper disable MemberCanBeProtected.Global

namespace PureES.EventStore.InMemory;

[SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
internal class InMemoryEventStore : IInMemoryEventStore
{
    private EventRecordList _events = EventRecordList.Empty;
    
    private readonly IEventTypeMap _eventTypeMap;

    private readonly InMemoryEventStoreSerializer _serializer;
    private readonly ISystemClock _clock;

    private readonly List<IInMemoryEventStoreSubscription> _subscriptions;

    public InMemoryEventStore(InMemoryEventStoreSerializer serializer,
        ISystemClock clock,
        IEventTypeMap eventTypeMap,
        IEnumerable<IHostedService>? hostedServices = null)
    {
        _serializer = serializer;
        _clock = clock;
        _eventTypeMap = eventTypeMap;

        _subscriptions = hostedServices?.OfType<IInMemoryEventStoreSubscription>().ToList() ?? [];
    }

    /// <summary>
    /// Posts events to <see cref="_subscriptions"/>
    /// </summary>
    private void AfterCommit(List<EventRecord> records)
    {
        foreach (var s in _subscriptions)
            s.AfterCommit(records);
    }
    
    #region Revision

    public Task<ulong> GetRevision(string streamId, CancellationToken _) =>
        _events.TryGetRevision(streamId, out var revision)
            ? Task.FromResult(revision)
            : Task.FromException<ulong>(new StreamNotFoundException(streamId));

    public Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken _)
    {
        if (!_events.TryGetRevision(streamId, out var actual))
            return Task.FromException<ulong>(new StreamNotFoundException(streamId));
        
        return actual == expectedRevision
            ? Task.FromResult(expectedRevision)
            : Task.FromException<ulong>(new WrongStreamRevisionException(streamId, expectedRevision, actual));
    }

    public Task<bool> Exists(string streamId, CancellationToken _) => Task.FromResult(_events.Exists(streamId));

    #endregion
    
    #region Write

    private void CreateRecords(string streamId,
        IEnumerable<UncommittedEvent> events,
        out List<EventRecord> records)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (events == null) throw new ArgumentNullException(nameof(events));

        var ts = _clock.UtcNow;
        records = events.Select(e => _serializer.Serialize(e, streamId, ts)).ToList();
        if (records.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(events));
    }
    
    private void CreateRecords(string streamId, 
        IEnumerable<UncommittedEvent> events,
        DateTimeOffset timestamp,
        out List<EventRecord> records)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (events == null) throw new ArgumentNullException(nameof(events));

        records = events.Select(e => _serializer.Serialize(e, streamId, timestamp)).ToList();
        if (records.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(events));
    }

    public Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        ulong revision;
        CreateRecords(streamId, events, out var records);
        
        lock (_events)
        {
            if (_events.Exists(streamId))
                return Task.FromException<ulong>(new StreamAlreadyExistsException(streamId));
            _events = _events.Append(streamId, records, out revision);
        }

        AfterCommit(records);
        return Task.FromResult(revision);
    }

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken _)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        return Create(streamId, [@event], _);
    }

    public Task<ulong> Append(string streamId, 
        ulong expectedRevision, 
        IEnumerable<UncommittedEvent> events,
        CancellationToken _)
    {
        ulong revision;
        CreateRecords(streamId, events, out var records);
        
        lock (_events)
        {
            if (!_events.TryGetRevision(streamId, out var actual))
                return Task.FromException<ulong>(new StreamNotFoundException(streamId));
            
            if (actual != expectedRevision)
                return Task.FromException<ulong>(new WrongStreamRevisionException(streamId, 
                    expectedRevision, actual));

            _events = _events.Append(streamId, records, out revision);
        }

        AfterCommit(records);
        return Task.FromResult(revision);
    }
    
    public Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        ulong revision;
        CreateRecords(streamId, events, out var records);
        
        lock (_events)
        {
            if (!_events.Exists(streamId))
                return Task.FromException<ulong>(new StreamNotFoundException(streamId));
            
            _events = _events.Append(streamId, records, out revision);
        }

        AfterCommit(records);
        return Task.FromResult(revision);
    }

    public Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event, CancellationToken _)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        return Append(streamId, expectedRevision, new [] { @event }, _);
    }

    public Task<ulong> Append(string streamId, UncommittedEvent @event, CancellationToken _)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        return Append(streamId, [ @event ], _);
    }

    public Task SubmitTransaction(IReadOnlyDictionary<string, UncommittedEventsList> transaction, CancellationToken _)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));
        
        if (transaction.Count == 0)
            return Task.CompletedTask; //No events to submit

        var allRecords = new List<EventRecord>();
        lock (_events)
        {
            var exceptions = new List<Exception>();
            foreach (var (streamId, list) in transaction)
            {
                if (_events.TryGetRevision(streamId, out var actual))
                {
                    if (list.ExpectedRevision == null)
                        exceptions.Add(new StreamAlreadyExistsException(streamId));
                    else if (actual != list.ExpectedRevision.Value)
                        exceptions.Add(new WrongStreamRevisionException(streamId, list.ExpectedRevision.Value, actual));
                }
                else if (list.ExpectedRevision != null)
                {
                    exceptions.Add(new StreamNotFoundException(streamId));
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

            var ts = _clock.UtcNow;
            
            foreach (var (streamId, list) in transaction)
            {
                CreateRecords(streamId, list.Events, ts, out var records);
                _events = _events.Append(streamId, records, out var _);
                allRecords.AddRange(records);
            }
        }

        AfterCommit(allRecords);

        return Task.CompletedTask;
    }

    #endregion
    
    #region Read

    public IEnumerable<EventEnvelope> ReadAll() => _events.Select(_serializer.Deserialize);
    
    private IAsyncEnumerable<EventEnvelope> ToAsyncEnumerable(IEnumerable<EventRecord> records)
    {
        return records.Select(_serializer.Deserialize).ToAsyncEnumerable();
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, CancellationToken cancellationToken)
    {
        return ToAsyncEnumerable(_events.ReadAll(direction));
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, 
        ulong maxCount, 
        CancellationToken cancellationToken)
    {
        return ToAsyncEnumerable(_events.ReadAll(direction, maxCount));
    }

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, CancellationToken _)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));

        return ToAsyncEnumerable(_events.ReadStream(direction, streamId, out var _));
    }

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, ulong expectedRevision, CancellationToken _)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        
        var records = _events.ReadStream(direction, streamId, out var actual);
        
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);

        return ToAsyncEnumerable(records);
    }

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, 
        string streamId, 
        ulong startRevision, 
        ulong expectedRevision,
        CancellationToken cancellationToken = default)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        
        if (startRevision > expectedRevision)
            throw new ArgumentOutOfRangeException(nameof(startRevision));
        
        var stream = _events.ReadStream(direction, streamId, out var actual);

        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);

        var skip = (int)startRevision;
        stream = direction switch
        {
            Direction.Forwards => stream.Skip(skip),
            Direction.Backwards => stream.SkipLast(skip),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        
        return ToAsyncEnumerable(stream);
    }

    public IAsyncEnumerable<EventEnvelope> ReadPartial(Direction direction, 
        string streamId,
        ulong count, 
        CancellationToken _)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));

        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        
        var stream = _events.ReadStream(direction, streamId, out var actual);
        if (actual < count - 1)
            throw new WrongStreamRevisionException(streamId, count, actual);

        return ToAsyncEnumerable(stream.Take((int)count));
    }

    public IAsyncEnumerable<EventEnvelope> ReadSlice(string streamId, 
        ulong startRevision, 
        ulong endRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        if (startRevision > endRevision)
            throw new ArgumentOutOfRangeException(nameof(startRevision));
        
        var stream = _events.ReadStream(Direction.Forwards, streamId, out var actual);

        if (endRevision > actual)
            throw new WrongStreamRevisionException(streamId, endRevision, actual);
        
        return ToAsyncEnumerable(stream.Take((int)endRevision + 1).Skip((int)startRevision));
    }

    public IAsyncEnumerable<EventEnvelope> ReadSlice(string streamId, ulong startRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var stream = _events.ReadStream(Direction.Forwards, streamId, out var actual);

        if (actual < startRevision)
            throw new WrongStreamRevisionException(streamId, startRevision, actual);
        
        return ToAsyncEnumerable(stream.Skip((int)startRevision));
    }

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, 
        IEnumerable<string> streams, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streams);

        streams = direction switch
        {
            Direction.Forwards => streams.OrderBy(s => s),
            Direction.Backwards => streams.OrderByDescending(s => s),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        var result =
            from s in streams
            where _events.Exists(s) //Ignore not found
            select ToAsyncEnumerable(_events.ReadStream(direction, s, out _));

        return result.ToAsyncEnumerable();
    }
    
    public async IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction,
        IAsyncEnumerable<string> streams,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        streams = direction switch
        {
            Direction.Forwards => streams.OrderBy(s => s),
            Direction.Backwards => streams.OrderByDescending(s => s),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        await foreach (var s in streams.WithCancellation(cancellationToken))
        {
            if (!_events.Exists(s))
                continue; //Ignore not found
            yield return ToAsyncEnumerable(_events.ReadStream(direction, s, out _));
        }
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, 
        Type eventType,
        CancellationToken cancellationToken)
    {
        var type = _eventTypeMap.GetTypeName(eventType);
        var records = _events.ReadAll(direction)
            .Where(e => e.EventType == type);

        return ToAsyncEnumerable(records);
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, 
        Type eventType,
        ulong maxCount,
        CancellationToken cancellationToken)
    {
        var type = _eventTypeMap.GetTypeName(eventType);

        var records = _events.ReadAll(direction)
            .Where(e => e.EventType == type)
            .Take((int)maxCount);

        return ToAsyncEnumerable(records);
    }

    #endregion
    
    #region Count

    public Task<ulong> CountByEventType(Type eventType, CancellationToken cancellationToken)
    {
        var type = _eventTypeMap.GetTypeName(eventType);
        var count = _events.Count(r => r.EventType == type);
        return Task.FromResult((ulong)count);
    }
    
    public Task<ulong> Count(CancellationToken cancellationToken) => Task.FromResult((ulong)_events.Count);

    public int GetCount() => _events.Count;

    #endregion
    
    #region Load

    private void LoadRecords(IEnumerable<EventRecord> records)
    {
        lock (_events)
        {
            if (_events.Count > 0)
                throw new InvalidOperationException("Event store already contains records");
            
            foreach (var e in records)
            {
                if (_events.TryGetRevision(e.StreamId, out var actual))
                {
                    var expected = (int)actual + 1;
                    if (e.StreamPos != expected)
                        throw new InvalidOperationException(
                            $"Stream {e.StreamId} is not sequential. Expected {expected}, got {e.StreamPos}");
                    
                    _events = _events.Append(e.StreamId, [e], out _);
                }
                else
                {
                    if (e.StreamPos != 0)
                        throw new InvalidOperationException($"Stream {e.StreamId} does not start at 0");
                    
                    _events = _events.Append(e.StreamId, [e], out _);
                }
            }
        }
    }
    
    public async Task Load(IAsyncEnumerable<EventEnvelope> envelopes, CancellationToken ct)
    {
        var records = await envelopes.Select(_serializer.Serialize).ToListAsync(ct);
        
        records = records
            .OrderBy(r => r.Timestamp)
            .ThenBy(r => r.StreamPos)
            .ToList();
        
        LoadRecords(records);
    }

    //For serializing, we just save a JSON array of EventRecords
    
    public JsonElement Serialize() => _events.Serialize();

    public void Deserialize(JsonElement events)
    {
        var records = events.Deserialize<IEnumerable<EventRecord>>();
        if (records == null)
            throw new ArgumentOutOfRangeException(nameof(events));
        LoadRecords(records);
    }

    #endregion
}
