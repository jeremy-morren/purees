using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Internal;
using ProtoBuf;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory.Serialization;
using PureES.EventStore.InMemory.Subscription;

// ReSharper disable MemberCanBeProtected.Global

namespace PureES.EventStore.InMemory;

internal class InMemoryEventStore : IInMemoryEventStore
{
    private readonly List<(string StreamId, int StreamPosition)> _all = new();
    private readonly Dictionary<string, List<EventRecord>> _events = new();
    private readonly IEventTypeMap _eventTypeMap;
    private readonly InMemoryEventStoreSubscriptionToAll? _subscription;

    private readonly InMemoryEventStoreSerializer _serializer;
    private readonly ISystemClock _systemClock;

    public InMemoryEventStore(InMemoryEventStoreSerializer serializer,
        ISystemClock systemClock,
        IEventTypeMap eventTypeMap,
        InMemoryEventStoreSubscriptionToAll? subscription = null)
    {
        _serializer = serializer;
        _systemClock = systemClock;
        _eventTypeMap = eventTypeMap;
        _subscription = subscription;
    }

    //Implementation of IEventSTore
    //Note that all methods have to be synchronized to maintain integrity
    
    #region Append

    public Task<ulong> GetRevision(string streamId, CancellationToken _)
    {
        lock (_events)
        {
            if (!_events.TryGetValue(streamId, out var events))
                throw new StreamNotFoundException(streamId);
            return Task.FromResult((ulong) events.Count - 1);
        }
    }
    
    public Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken _)
    {
        lock (_events)
        {
            if (!_events.TryGetValue(streamId, out var current))
                throw new StreamNotFoundException(streamId);
            if (current.Count - 1 != (long) expectedRevision)
                throw new WrongStreamRevisionException(streamId,
                    expectedRevision,
                    (ulong) current.Count - 1);
            return Task.FromResult((ulong) current.Count - 1);
        }
    }

    public Task<bool> Exists(string streamId, CancellationToken _)
    {
        lock (_events)
        {
            return Task.FromResult(_events.ContainsKey(streamId));
        }
    }

    public Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        lock (_events)
        {
            if (_events.TryGetValue(streamId, out var current))
                throw new StreamAlreadyExistsException(streamId, (ulong) current.Count - 1);
            _events.Add(streamId, new List<EventRecord>());
            return Task.FromResult(AppendStream(streamId, events));
        }
    }

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken _)
        => Create(streamId, ImmutableArray.Create(@event), _);

    public Task<ulong> Append(string streamId, ulong expectedRevision, IEnumerable<UncommittedEvent> events,
        CancellationToken _)
    {
        lock (_events)
        {
            if (!_events.TryGetValue(streamId, out var current))
                throw new StreamNotFoundException(streamId);
            if (current.Count - 1 != (long) expectedRevision)
                throw new WrongStreamRevisionException(streamId,
                    expectedRevision,
                    (ulong) current.Count - 1);
            return Task.FromResult(AppendStream(streamId, events));
        }
    }
    
    public Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        lock (_events)
        {
            if (!_events.ContainsKey(streamId))
                throw new StreamNotFoundException(streamId);
            return Task.FromResult(AppendStream(streamId, events));
        }
    }

    public Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event, CancellationToken _)
        => Append(streamId, expectedRevision, ImmutableArray.Create(@event), _);
    
    public Task<ulong> Append(string streamId, UncommittedEvent @event, CancellationToken _)
        => Append(streamId, ImmutableArray.Create(@event), _);

    private ulong AppendStream(string streamId, IEnumerable<UncommittedEvent> events)
    {
        var timestamp = _systemClock.UtcNow;
        var current = _events[streamId];
        var toPublish = new List<EventRecord>();
        foreach (var e in events)
        {
            var record = _serializer.Serialize(e, streamId, timestamp);
            var streamPos = current.Count;

            record.StreamPosition = (uint) streamPos;

            current.Add(record);
            _all.Add((streamId, streamPos));
            toPublish.Add(record);
        }

        _subscription?.Publish(toPublish);
        return (ulong) (current.Count - 1);
    }
    
    #endregion
    
    #region Read

    public IReadOnlyList<EventEnvelope> ReadAll()
    {
        lock (_events)
        {
            var list = new List<EventEnvelope>();
            foreach (var (streamId, streamIndex) in _all)
            {
                var stream = _events[streamId];
                list.Add(_serializer.Deserialize(stream[streamIndex]));
            }
            return list;
        }
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(CancellationToken cancellationToken) =>
        ReadAll().ToAsyncEnumerable();

    public IAsyncEnumerable<EventEnvelope> Read(string streamId, CancellationToken _)
    {
        lock (_events)
        {
            if (!_events.TryGetValue(streamId, out var events))
                throw new StreamNotFoundException(streamId);
            var list = Enumerable.Range(0, events.Count)
                .Select(i => events[i])
                .Select(_serializer.Deserialize)
                .ToList();

            return list.ToAsyncEnumerable();
        }
    }

    public IAsyncEnumerable<EventEnvelope> Read(string streamId, ulong expectedRevision, CancellationToken _)
    {
        lock (_events)
        {
            if (!_events.TryGetValue(streamId, out var events))
                throw new StreamNotFoundException(streamId);
            var length = (ulong) events.Count - 1;
            if (length != expectedRevision)
                throw new WrongStreamRevisionException(streamId,
                    expectedRevision,
                    length);
            var list = Enumerable.Range(0, events.Count)
                .Select(i => events[i])
                .Select(_serializer.Deserialize)
                .ToList();

            return list.ToAsyncEnumerable();
        }
    }

    public IAsyncEnumerable<EventEnvelope> ReadPartial(string streamId, ulong requiredRevision, CancellationToken _)
    {
        lock (_events)
        {
            if (!_events.TryGetValue(streamId, out var events))
                throw new StreamNotFoundException(streamId);
            var length = (ulong) events.Count - 1;
            if (length < requiredRevision)
                throw new WrongStreamRevisionException(streamId,
                    requiredRevision,
                    length);
            var list = Enumerable.Range(0, (int) requiredRevision + 1)
                .Select(i => events[i])
                .Select(_serializer.Deserialize)
                .ToList();

            return list.ToAsyncEnumerable();
        }
    }

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Type eventType, CancellationToken cancellationToken)
    {
        lock (_events)
        {
            var name = _eventTypeMap.GetTypeName(eventType);
            var list = new List<EventEnvelope>();
            foreach (var (streamId, streamIndex) in _all)
            {
                var record = _events[streamId][streamIndex];
                if (record.EventType != name)
                    continue;
                var env = _serializer.Deserialize(record);
                list.Add(env);
            }

            return list.ToAsyncEnumerable();
        }
    }
    
    private List<EventEnvelope> ReadInternal(string streamId)
    {
        if (!_events.TryGetValue(streamId, out var events))
            throw new StreamNotFoundException(streamId);
        return Enumerable.Range(0, events.Count)
            .Select(i => events[i])
            .Select(_serializer.Deserialize)
            .ToList();
    }

    public IAsyncEnumerable<EventEnvelope> ReadMany(IEnumerable<string> streams, CancellationToken cancellationToken)
    {
        lock (_events)
        {
            var events = streams.SelectMany(ReadInternal)
                .OrderBy(e => e.Timestamp)
                .ToList();
            cancellationToken.ThrowIfCancellationRequested();
            return events.ToAsyncEnumerable();
        }
    }

    public async IAsyncEnumerable<EventEnvelope> ReadMany(IAsyncEnumerable<string> streams,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var list = await streams.ToListAsync(cancellationToken);

        List<EventEnvelope> events;
        lock (_events)
        {
            events = list.SelectMany(ReadInternal)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }

        foreach (var e in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return e;
        }
    }

    #endregion

    #region Persistence

    //These methods are intended to facilitate unit tests

    private const PrefixStyle PrefixStyle = ProtoBuf.PrefixStyle.Base128;

    public void Save(Stream stream, CancellationToken cancellationToken = default)
    {
        lock (_events)
        {
            foreach (var (streamId, streamIndex) in _all)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var record = _events[streamId][streamIndex];
                Serializer.SerializeWithLengthPrefix(stream, record, PrefixStyle, 1);
            }

            stream.Flush();
        }
    }

    public void Load(Stream stream, CancellationToken cancellationToken = default)
    {
        lock (_events)
        {
            var events = Serializer.DeserializeItems<EventRecord>(stream, PrefixStyle, 1);
            foreach (var e in events)
            {
                //For some reason, protobuf does not preserve Kind on serialization
                e.Created = DateTime.SpecifyKind(e.Created, DateTimeKind.Utc);
                var streamId = e.StreamId;
                if (!_events.TryGetValue(streamId, out var eventStream))
                {
                    eventStream = new List<EventRecord>();
                    _events.Add(streamId, eventStream);
                }
                e.StreamPosition = (uint) eventStream.Count;
                _all.Add((streamId, eventStream.Count));
                eventStream.Add(e);
            }
        }
    }

    #endregion
}