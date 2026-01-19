using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;
using PureES.EventStore.InMemory.Subscription;

// ReSharper disable MemberCanBeProtected.Global

namespace PureES.EventStore.InMemory;

[SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
internal class InMemoryEventStore : IInMemoryEventStore
{
    private EventRecordList _events = EventRecordList.Empty;
    
    private readonly IEventTypeMap _eventTypeMap;

    private readonly InMemoryEventStoreSerializer _serializer;
    private readonly TimeProvider _clock;

    private readonly List<IInMemoryEventStoreSubscription> _subscriptions;

    public InMemoryEventStore(InMemoryEventStoreSerializer serializer,
        TimeProvider clock,
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
    private void AfterCommit(List<InMemoryEventRecord> records)
    {
        foreach (var s in _subscriptions)
            s.AfterCommit(records);
    }

    #region Revision

    public Task<uint> GetRevision(string streamId, CancellationToken _) =>
        _events.TryGetRevision(streamId, out var revision)
            ? Task.FromResult(revision)
            : Task.FromException<uint>(new StreamNotFoundException(streamId));

    public Task<uint> GetRevision(string streamId, uint expectedRevision, CancellationToken _)
    {
        if (!_events.TryGetRevision(streamId, out var actual))
            return Task.FromException<uint>(new StreamNotFoundException(streamId));

        return actual == expectedRevision
            ? Task.FromResult(expectedRevision)
            : Task.FromException<uint>(new WrongStreamRevisionException(streamId, expectedRevision, actual));
    }

    public Task<bool> Exists(string streamId, CancellationToken _) => Task.FromResult(_events.Exists(streamId));

    #endregion

    #region Write

    private void CreateRecords(string streamId,
        IEnumerable<UncommittedEvent> events,
        out List<InMemoryEventRecord> records)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(events);

        var ts = _clock.GetLocalNow();
        records = events.Select(e => _serializer.Serialize(e, streamId, ts)).ToList();
        if (records.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(events));
    }

    private void CreateRecords(string streamId,
        IEnumerable<UncommittedEvent> events,
        DateTimeOffset timestamp,
        out List<InMemoryEventRecord> records)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (events == null) throw new ArgumentNullException(nameof(events));

        records = events.Select(e => _serializer.Serialize(e, streamId, timestamp)).ToList();
        if (records.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(events));
    }

    public Task<uint> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        uint revision;
        CreateRecords(streamId, events, out var records);

        lock (_events)
        {
            if (_events.Exists(streamId))
                return Task.FromException<uint>(new StreamAlreadyExistsException(streamId));
            _events = _events.Append(streamId, records, out revision);
        }

        AfterCommit(records);
        return Task.FromResult(revision);
    }

    public Task<uint> Create(string streamId, UncommittedEvent @event, CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return Create(streamId, [@event], _);
    }

    public Task<uint> Append(string streamId,
        uint expectedRevision,
        IEnumerable<UncommittedEvent> events,
        CancellationToken _)
    {
        uint revision;
        CreateRecords(streamId, events, out var records);

        lock (_events)
        {
            if (!_events.TryGetRevision(streamId, out var actual))
                return Task.FromException<uint>(new StreamNotFoundException(streamId));

            if (actual != expectedRevision)
                return Task.FromException<uint>(new WrongStreamRevisionException(streamId,
                    expectedRevision, actual));

            _events = _events.Append(streamId, records, out revision);
        }

        AfterCommit(records);
        return Task.FromResult(revision);
    }

    public Task<uint> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        uint revision;
        CreateRecords(streamId, events, out var records);

        lock (_events)
        {
            if (!_events.Exists(streamId))
                return Task.FromException<uint>(new StreamNotFoundException(streamId));

            _events = _events.Append(streamId, records, out revision);
        }

        AfterCommit(records);
        return Task.FromResult(revision);
    }

    public Task<uint> Append(string streamId, uint expectedRevision, UncommittedEvent @event, CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return Append(streamId, expectedRevision, [@event], _);
    }

    public Task<uint> Append(string streamId, UncommittedEvent @event, CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return Append(streamId, [ @event ], _);
    }

    public Task SubmitTransaction(IReadOnlyList<UncommittedEventsList> transaction, CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Count == 0)
            return Task.CompletedTask; //No events to submit

        var allRecords = new List<InMemoryEventRecord>();
        lock (_events)
        {
            var exceptions = new List<Exception>();
            foreach (var (streamId, expectedRevision, _) in transaction)
            {
                if (_events.TryGetRevision(streamId, out var actual))
                {
                    if (expectedRevision == null)
                        exceptions.Add(new StreamAlreadyExistsException(streamId));
                    else if (actual != expectedRevision.Value)
                        exceptions.Add(new WrongStreamRevisionException(streamId, expectedRevision.Value, actual));
                }
                else if (expectedRevision != null)
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

            var ts = _clock.GetLocalNow();

            foreach (var (streamId, _, events) in transaction)
            {
                CreateRecords(streamId, events, ts, out var records);
                _events = _events.Append(streamId, records, out var _);
                allRecords.AddRange(records);
            }
        }

        AfterCommit(allRecords);

        return Task.CompletedTask;
    }

    #endregion

    #region Read Sync

    public IEnumerable<EventEnvelope> ReadAllSync() => _events.Select(_serializer.Deserialize);

    public IEnumerable<EventEnvelope> ReadSync(Direction direction, string streamId) =>
        _events.ReadStream(direction, streamId, out _).Select(_serializer.Deserialize);

    public bool ExistsSync(string streamId) => _events.Exists(streamId);

    public IEnumerable<EventEnvelope> ReadByEventTypeSync(Direction direction, Type[] eventTypes)
    {
        var types = GetTypeNames(eventTypes);
        return _events.ReadAll(direction)
            .Where(r => r.TypeContains(types))
            .Select(_serializer.Deserialize);
    }

    #endregion

    #region Read

    private IAsyncEnumerable<EventEnvelope> ToAsyncEnumerable(IEnumerable<InMemoryEventRecord> records) =>
        records.Select(_serializer.Deserialize).ToAsyncEnumerable();

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, CancellationToken cancellationToken) =>
        ToAsyncEnumerable(_events.ReadAll(direction));

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction,
        uint maxCount,
        CancellationToken cancellationToken)
    {
        return ToAsyncEnumerable(_events.ReadAll(direction, maxCount));
    }

    public IEventStoreStream Read(Direction direction, string streamId, CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var records = _events.ReadStream(direction, streamId, out var _);
        return new InMemoryEventStream(direction, streamId, records, _serializer);
    }


    public IEventStoreStream Read(Direction direction, string streamId, uint expectedRevision, CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var records = _events.ReadStream(direction, streamId, out var actual);

        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);

        return new InMemoryEventStream(direction, streamId, records, _serializer);
    }

    public IEventStoreStream Read(Direction direction,
        string streamId,
        uint startRevision,
        uint expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, expectedRevision);

        var records = _events.ReadStream(direction, streamId, out var actual);

        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);

        var skip = (int)startRevision;
        records = direction switch
        {
            Direction.Forwards => records.Skip(skip),
            Direction.Backwards => records.SkipLast(skip),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        return new InMemoryEventStream(direction, streamId, records, _serializer);
    }

    public IEventStoreStream ReadPartial(Direction direction,
        string streamId,
        uint count,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 0u);

        var stream = _events.ReadStream(direction, streamId, out var actual);
        if (actual < count - 1)
            throw new WrongStreamRevisionException(streamId, count - 1, actual);

        return new InMemoryEventStream(direction, streamId, stream.Take((int)count), _serializer);
    }

    public IEventStoreStream ReadSlice(string streamId,
        uint startRevision,
        uint endRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startRevision, endRevision);

        var stream = _events.ReadStream(Direction.Forwards, streamId, out var actual);

        if (endRevision > actual)
            throw new WrongStreamRevisionException(streamId, endRevision, actual);

        return new InMemoryEventStream(Direction.Forwards,
            streamId,
            stream.Take((int)endRevision + 1).Skip((int)startRevision),
            _serializer);
    }

    public IEventStoreStream ReadSlice(string streamId, uint startRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamId);

        var stream = _events.ReadStream(Direction.Forwards, streamId, out var actual);

        if (actual < startRevision)
            throw new WrongStreamRevisionException(streamId, startRevision, actual);

        return new InMemoryEventStream(Direction.Forwards,
            streamId,
            stream.Skip((int)startRevision),
            _serializer);
    }

    public IAsyncEnumerable<IEventStoreStream> ReadMany(Direction direction,
        IEnumerable<string> streams,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(streams);

        streams = streams.Distinct();

        streams = direction switch
        {
            Direction.Forwards => streams.OrderBy(s => s),
            Direction.Backwards => streams.OrderByDescending(s => s),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        var list = streams.ToList();

        var exceptions = list
            .Where(id => !_events.Exists(id))
            .Select(id => new StreamNotFoundException(id))
            .ToList();

        // Handle streams not found
        switch (exceptions.Count)
        {
            case 0:
                break;
            case 1:
                throw exceptions[0];
            default:
                throw new AggregateException(exceptions);
        }

        var result =
            from id in list
            let stream = _events.ReadStream(direction, id, out _)
            select new InMemoryEventStream(direction, id, stream, _serializer);

        return result.ToAsyncEnumerable();
    }

    public async IAsyncEnumerable<IEventStoreStream> ReadMany(Direction direction,
        IAsyncEnumerable<string> streams,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var list = await streams.ToListAsync(cancellationToken);
        await foreach (var stream in ReadMany(direction, list, cancellationToken))
            yield return stream;
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction,
        Type[] eventTypes,
        CancellationToken cancellationToken)
    {
        return ReadByEventTypeSync(direction, eventTypes).ToAsyncEnumerable();
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction,
        Type[] eventTypes,
        uint maxCount,
        CancellationToken cancellationToken)
    {
        return ReadByEventTypeSync(direction, eventTypes).Take((int)maxCount).ToAsyncEnumerable();
    }

    #endregion

    #region Count

    public Task<uint> CountByEventType(Type[] eventTypes, CancellationToken cancellationToken)
    {
        return Task.FromResult(CountByEventTypeSync(eventTypes));
    }

    public Task<uint> Count(CancellationToken cancellationToken) => Task.FromResult(GetCountSync());

    public uint CountByEventTypeSync(Type[] eventTypes)
    {
        var types = GetTypeNames(eventTypes);
        return (uint)_events.Count(r => r.TypeContains(types));
    }

    public uint GetCountSync() => (uint)_events.Count;

    #endregion

    private HashSet<string> GetTypeNames(Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        return eventTypes
            .Distinct()
            .Select(_eventTypeMap.GetTypeNames)
            .Select(l => l[^1])
            .ToHashSet();
    }

    #region Load & Save

    private void Load(IReadOnlyList<SerializedInMemoryEventRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
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

                    _events = _events.Append(e.StreamId, [e.Source], out _);
                }
                else
                {
                    if (e.StreamPos != 0)
                        throw new InvalidOperationException($"Stream {e.StreamId} does not start at 0");

                    _events = _events.Append(e.StreamId, [e.Source], out _);
                }
            }
        }
    }

    public async Task Load(IAsyncEnumerable<EventEnvelope> events, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);
        var records = await events
            .Select(_serializer.Serialize)
            .Select(r => new SerializedInMemoryEventRecord(r))
            .ToListAsync(ct);

        Load(records);
    }

    public async Task Load(IAsyncEnumerable<SerializedInMemoryEventRecord> events, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);
        Load(await events.ToListAsync(ct));
    }

    public void Load(IEnumerable<SerializedInMemoryEventRecord> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Load(events as IReadOnlyList<SerializedInMemoryEventRecord> ?? events.ToList());
    }

    public IEnumerable<SerializedInMemoryEventRecord> Serialize() =>
        _events.Records.Select(r => new SerializedInMemoryEventRecord(r));

    #endregion
}
