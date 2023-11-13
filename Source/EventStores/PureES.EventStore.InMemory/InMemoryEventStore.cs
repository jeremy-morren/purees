using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Internal;
using PureES.Core;
using PureES.EventStore.InMemory.Subscription;

// ReSharper disable MemberCanBeProtected.Global

namespace PureES.EventStore.InMemory;

internal class InMemoryEventStore : IInMemoryEventStore
{
    private readonly object _mutex = new();
    private ImmutableList<EventRecord> _records = ImmutableList<EventRecord>.Empty;
    private readonly ConcurrentDictionary<string, IndexList> _streams = new();
    
    private readonly IEventTypeMap _eventTypeMap;

    private readonly InMemoryEventStoreSerializer _serializer;
    private readonly ISystemClock _systemClock;

    private readonly List<IInMemoryEventStoreSubscription> _subscriptions;

    public InMemoryEventStore(InMemoryEventStoreSerializer serializer,
        ISystemClock systemClock,
        IEventTypeMap eventTypeMap,
        IEnumerable<IHostedService>? hostedServices = null)
    {
        _serializer = serializer;
        _systemClock = systemClock;
        _eventTypeMap = eventTypeMap;

        _subscriptions = hostedServices?.OfType<IInMemoryEventStoreSubscription>().ToList() ??
                         new List<IInMemoryEventStoreSubscription>();
    }

    private class IndexList
    {
        /// <summary>
        /// Indexes of events inside the _records list
        /// </summary>
        public required ImmutableList<int> Indexes;
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

    public Task<ulong> GetRevision(string streamId, CancellationToken _)
    {
        return _streams.TryGetValue(streamId, out var s)
            ? Task.FromResult((ulong)s.Indexes.Count - 1)
            : Task.FromException<ulong>(new StreamNotFoundException(streamId));
    }
    
    public Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken _)
    {
        if (!_streams.TryGetValue(streamId, out var current))
            return Task.FromException<ulong>(new StreamNotFoundException(streamId));

        var actual = current.Indexes.Count - 1;
        
        return actual == (int)expectedRevision
            ? Task.FromResult(expectedRevision)
            : Task.FromException<ulong>(new WrongStreamRevisionException(streamId, expectedRevision, (ulong)actual));
    }

    public Task<bool> Exists(string streamId, CancellationToken _)
    {
        return Task.FromResult(_streams.ContainsKey(streamId));
    }
    
    #endregion
    
    #region Write

    private void CreateRecords(string streamId, 
        int startPos, 
        IEnumerable<UncommittedEvent> events,
        out List<EventRecord> records,
        out Task<ulong> result)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (events == null) throw new ArgumentNullException(nameof(events));

        var ts = _systemClock.UtcNow;
        records = events.Select(e => _serializer.Serialize(e, streamId, startPos++, ts)).ToList();
        result = Task.FromResult((ulong)startPos - 1);
    }

    public Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        CreateRecords(streamId, 0, events, out var records, out var result);
        
        lock (_mutex)
        {
            if (_streams.ContainsKey(streamId))
                return Task.FromException<ulong>(new StreamAlreadyExistsException(streamId));
            _streams[streamId] = new IndexList()
            {
                Indexes = ImmutableList.CreateRange(Enumerable.Range(_records.Count, records.Count))
            };
            _records = _records.AddRange(records);
        }

        AfterCommit(records);
        return result;
    }

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken _)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        return Create(streamId, new []{ @event }, _);
    }

    public Task<ulong> Append(string streamId, 
        ulong expectedRevision, 
        IEnumerable<UncommittedEvent> events,
        CancellationToken _)
    {
        CreateRecords(streamId, (int)expectedRevision + 1, events, out var records, out var result);
        
        lock (_mutex)
        {
            if (!_streams.TryGetValue(streamId, out var current))
                return Task.FromException<ulong>(new StreamNotFoundException(streamId));
            if (current.Indexes.Count - 1 != (int)expectedRevision)
                return Task.FromException<ulong>(new WrongStreamRevisionException(streamId, 
                    expectedRevision, (ulong)current.Indexes.Count - 1));
            
            current.Indexes = current.Indexes.AddRange(Enumerable.Range(_records.Count, records.Count));
            _streams[streamId] = current;
            
            _records = _records.AddRange(records);
        }

        AfterCommit(records);
        return result;
    }
    
    public Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        CreateRecords(streamId, 0, events, out var records, out var result);
        lock (_mutex)
        {
            if (!_streams.TryGetValue(streamId, out var current))
                return Task.FromException<ulong>(new StreamNotFoundException(streamId));
            
            foreach (var r in records)
                r.StreamPos += current.Indexes.Count; //Update to actual position
            
            current.Indexes = current.Indexes.AddRange(Enumerable.Range(_records.Count, records.Count));
            _streams[streamId] = current;
            
            _records = _records.AddRange(records);
            
            result = Task.FromResult((ulong)current.Indexes.Count - 1);
        }
        
        AfterCommit(records);
        return result;
    }

    public Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event, CancellationToken _)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        return Append(streamId, expectedRevision, new [] { @event }, _);
    }

    public Task<ulong> Append(string streamId, UncommittedEvent @event, CancellationToken _)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        
        return Append(streamId, new [] { @event }, _);
    }
    
    #endregion
    
    #region Read

    public IReadOnlyList<EventEnvelope> ReadAll()
    {
        return _records.Select(_serializer.Deserialize).ToList();
    }

    private static IEnumerable<int> GetIndexes(Direction direction, IEnumerable<int> indexes)
    {
        return direction switch
        {
            Direction.Forwards => indexes,
            Direction.Backwards => indexes.Reverse(),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }
    
    private IAsyncEnumerable<EventEnvelope> ToAsyncEnumerable(IEnumerable<int> indexes, int? maxCount = null)
    {
        var e = indexes.Select(i => _serializer.Deserialize(_records[i]));
        if (maxCount.HasValue)
            e = e.TakeWhile((_, i) => i < maxCount.Value);
        return e.ToAsyncEnumerable();
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, CancellationToken cancellationToken)
    {
        return ToAsyncEnumerable(GetIndexes(direction, Enumerable.Range(0, _records.Count)));
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, 
        ulong maxCount, 
        CancellationToken cancellationToken)
    {
        return ToAsyncEnumerable(GetIndexes(direction, Enumerable.Range(0, _records.Count)), (int)maxCount);
    }

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, CancellationToken _)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        
        if (!_streams.TryGetValue(streamId, out var stream))
            throw new StreamNotFoundException(streamId);
        return ToAsyncEnumerable(GetIndexes(direction, stream.Indexes));
    }

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, ulong expectedRevision, CancellationToken _)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        
        if (!_streams.TryGetValue(streamId, out var stream))
            throw new StreamNotFoundException(streamId);
        
        var indexes = stream.Indexes;
        if (indexes.Count - 1 != (int)expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, (ulong)indexes.Count - 1);
        return ToAsyncEnumerable(GetIndexes(direction, indexes));
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
        
        if (!_streams.TryGetValue(streamId, out var stream))
            throw new StreamNotFoundException(streamId);
        
        var indexes = stream.Indexes;
        if (indexes.Count - 1 != (int)expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, (ulong)indexes.Count - 1);

        return ToAsyncEnumerable(indexes.Skip((int)startRevision));
    }

    public IAsyncEnumerable<EventEnvelope> ReadPartial(Direction direction, 
        string streamId,
        ulong count, 
        CancellationToken _)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));

        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        
        if (!_streams.TryGetValue(streamId, out var stream))
            throw new StreamNotFoundException(streamId);

        var indexes = stream.Indexes;
        if (indexes.Count <= (int)count)
            throw new WrongStreamRevisionException(streamId, count, (ulong)indexes.Count - 1);
        
        return ToAsyncEnumerable(GetIndexes(direction, indexes).Take((int)count));
    }

    public IAsyncEnumerable<EventEnvelope> ReadSlice(string streamId, 
        ulong startRevision, 
        ulong endRevision,
        CancellationToken cancellationToken = default)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));

        if (startRevision > endRevision)
            throw new ArgumentOutOfRangeException(nameof(startRevision));
        
        if (!_streams.TryGetValue(streamId, out var stream))
            throw new StreamNotFoundException(streamId);

        var indexes = stream.Indexes;
        if (indexes.Count <= (int)endRevision)
            throw new WrongStreamRevisionException(streamId, endRevision, (ulong)indexes.Count - 1);
        
        return ToAsyncEnumerable(indexes.Take((int)endRevision + 1).Skip((int)startRevision));
    }

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, 
        IEnumerable<string> streams, 
        CancellationToken cancellationToken)
    {
        if (streams == null) throw new ArgumentNullException(nameof(streams));

        streams = direction switch
        {
            Direction.Forwards => streams.OrderBy(s => s),
            Direction.Backwards => streams.OrderByDescending(s => s),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        
        var result = new List<IAsyncEnumerable<EventEnvelope>>();
        foreach (var s in streams)
        {
            if (!_streams.TryGetValue(s, out var stream))
                continue;
            result.Add(ToAsyncEnumerable(GetIndexes(direction, stream.Indexes)));
        }
        
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
            if (!_streams.TryGetValue(s, out var stream))
                continue;
            yield return ToAsyncEnumerable(GetIndexes(direction, stream.Indexes));
        }
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, 
        Type eventType,
        CancellationToken cancellationToken)
    {
        var type = _eventTypeMap.GetTypeName(eventType);
        var indexes = GetIndexes(direction, Enumerable.Range(0, _records.Count))
            .Where(i => _records[i].EventType == type);

        return ToAsyncEnumerable(indexes);
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, 
        Type eventType,
        ulong maxCount,
        CancellationToken cancellationToken)
    {
        var type = _eventTypeMap.GetTypeName(eventType);
        var indexes = GetIndexes(direction, Enumerable.Range(0, _records.Count))
            .Where(i => _records[i].EventType == type);

        return ToAsyncEnumerable(indexes, (int)maxCount);
    }

    #endregion
    
    #region Count

    public Task<ulong> CountByEventType(Type eventType, CancellationToken cancellationToken)
    {
        var type = _eventTypeMap.GetTypeName(eventType);
        var count = _records.Count(r => r.EventType == type);
        return Task.FromResult((ulong)count);
    }
    
    public Task<ulong> Count(CancellationToken cancellationToken)
    {
        var count = _records.Count;
        return Task.FromResult((ulong)count);
    }

    #endregion
    
    #region Load

    private void LoadRecords(List<EventRecord> records)
    {
        lock (_mutex)
        {
            if (_records.Count > 0)
                throw new InvalidOperationException("Event store already contains records");
            
            foreach (var e in records)
            {
                if (_streams.TryGetValue(e.StreamId, out var stream))
                {
                    var end = _records[stream.Indexes[^1]];
                    if (e.StreamPos != end.StreamPos + 1)
                        throw new InvalidOperationException($"Stream {e.StreamId} is not sequential");
                    stream.Indexes = stream.Indexes.Add(_records.Count);
                }
                else
                {
                    if (e.StreamPos != 0)
                        throw new InvalidOperationException($"Stream {e.StreamId} does not start at 0");
                    _streams[e.StreamId] = new IndexList
                    {
                        Indexes = ImmutableList.Create(_records.Count)
                    };
                }
                _records = _records.Add(e);
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

    public IReadOnlyList<JsonElement> Serialize()
    {
        return _records.Select(e => JsonSerializer.SerializeToElement(e)).ToList();
    }

    public void Deserialize(IEnumerable<JsonElement> events)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));
        var records = events.Select(e => e.Deserialize<EventRecord>()!).ToList();
        if (records == null)
            throw new ArgumentOutOfRangeException(nameof(events));
        LoadRecords(records);
    }

    //For serializing, we just save a JSON array of EventRecords

    public async Task Save(Stream stream, CompressionLevel compressionLevel, CancellationToken ct = default)
    {
        await using var brotli = new BrotliStream(stream, compressionLevel, leaveOpen: true);
        await JsonSerializer.SerializeAsync(brotli, _records, cancellationToken: ct);
    }

    public async Task Load(Stream stream, CancellationToken ct = default)
    {
        try
        {
            await using var brotli = new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true);
            var records = await JsonSerializer.DeserializeAsync<List<EventRecord>>(brotli, cancellationToken: ct);
            if (records == null)
                throw new ArgumentException("Stream does not contain a valid JSON array of EventRecords", nameof(stream));
            LoadRecords(records);
        }
        catch (Exception e)
        {
            throw new ArgumentException("Stream does not contain a valid JSON array of EventRecords", nameof(stream), e);
        }
    }

    #endregion
}
