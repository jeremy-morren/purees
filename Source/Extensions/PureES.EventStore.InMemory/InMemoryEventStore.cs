using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Internal;
using ProtoBuf;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.Core.EventStore.Serialization;
using PureES.EventStore.InMemory.Serialization;

// ReSharper disable MemberCanBeProtected.Global

namespace PureES.EventStore.InMemory;

internal class InMemoryEventStore : IInMemoryEventStore
{
    private readonly List<(string StreamId, int StreamPosition)> _all = new();
    private readonly Dictionary<string, List<EventRecord>> _events = new();
    private readonly IEventTypeMap _eventTypeMap;

    private readonly IInMemoryEventStoreSerializer _serializer;
    private readonly ISystemClock _systemClock;

    public InMemoryEventStore(IInMemoryEventStoreSerializer serializer,
        ISystemClock systemClock,
        IEventTypeMap eventTypeMap)
    {
        _serializer = serializer;
        _systemClock = systemClock;
        _eventTypeMap = eventTypeMap;
    }

    #region Implementation

    //Implementation of IEventSTore
    //Note that all methods have to be synchronized to maintain integrity

    [MethodImpl(MethodImplOptions.Synchronized)]
    public Task<ulong> GetRevision(string streamId, CancellationToken _)
    {
        if (!_events.TryGetValue(streamId, out var events))
            throw new StreamNotFoundException(streamId);
        return Task.FromResult((ulong) events.Count - 1);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public Task<bool> Exists(string streamId, CancellationToken _) => Task.FromResult(_events.ContainsKey(streamId));

    [MethodImpl(MethodImplOptions.Synchronized)]
    public Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        if (_events.TryGetValue(streamId, out var current))
            throw new StreamAlreadyExistsException(streamId, (ulong) current.Count - 1);
        _events.Add(streamId, new List<EventRecord>());
        return Task.FromResult(AppendStream(streamId, events));
    }

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken _)
        => Create(streamId, ImmutableArray.Create(@event), _);

    [MethodImpl(MethodImplOptions.Synchronized)]
    public Task<ulong> Append(string streamId, ulong expectedVersion, IEnumerable<UncommittedEvent> events,
        CancellationToken _)
    {
        if (!_events.TryGetValue(streamId, out var current))
            throw new StreamNotFoundException(streamId);
        if (current.Count - 1 != (long) expectedVersion)
            throw new WrongStreamRevisionException(streamId,
                expectedVersion,
                (ulong) current.Count - 1);
        return Task.FromResult(AppendStream(streamId, events));
    }

    public Task<ulong> Append(string streamId, ulong expectedVersion, UncommittedEvent @event, CancellationToken _)
        => Append(streamId, expectedVersion, ImmutableArray.Create(@event), _);

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IReadOnlyList<EventEnvelope> ReadAll()
    {
        var list = new List<EventEnvelope>();
        foreach (var (streamId, streamIndex) in _all)
        {
            var stream = _events[streamId];
            list.Add(_serializer.Deserialize(stream[streamIndex]));
        }

        return list;
    }

    public IAsyncEnumerable<EventEnvelope> ReadAll(CancellationToken cancellationToken) =>
        ReadAll().ToAsyncEnumerable();

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IAsyncEnumerable<EventEnvelope> Read(string streamId, CancellationToken _)
    {
        if (!_events.TryGetValue(streamId, out var events))
            throw new StreamNotFoundException(streamId);
        var list = Enumerable.Range(0, events.Count)
            .Select(i => events[i])
            .Select(_serializer.Deserialize)
            .ToList();

        return list.ToAsyncEnumerable();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IAsyncEnumerable<EventEnvelope> Read(string streamId, ulong expectedRevision, CancellationToken _)
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

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IAsyncEnumerable<EventEnvelope> ReadPartial(string streamId, ulong requiredRevision, CancellationToken _)
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

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Type eventType, CancellationToken cancellationToken)
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

    private ulong AppendStream(string streamId, IEnumerable<UncommittedEvent> events)
    {
        var timestamp = _systemClock.UtcNow;
        var current = _events[streamId];
        foreach (var e in events)
        {
            var record = _serializer.Serialize(e, streamId, timestamp);
            var streamPos = current.Count;

            record.StreamPosition = (uint) streamPos;
            record.OverallPosition = (uint) _all.Count;

            current.Add(record);
            _all.Add((streamId, streamPos));
        }

        return (ulong) (current.Count - 1);
    }

    #endregion

    #region Persistence

    //These methods are intended to facilitate unit tests

    private const PrefixStyle PrefixStyle = ProtoBuf.PrefixStyle.Base128;

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Save(Stream stream, CancellationToken cancellationToken = default)
    {
        foreach (var (streamId, streamIndex) in _all)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = _events[streamId][streamIndex];
            Serializer.SerializeWithLengthPrefix(stream, record, PrefixStyle, 1);
        }

        stream.Flush();
    }

    private void Add(EventRecord record)
    {
        var streamId = record.StreamId;
        if (!_events.TryGetValue(streamId, out var eventStream))
        {
            eventStream = new List<EventRecord>();
            _events.Add(streamId, eventStream);
        }

        record.StreamPosition = (uint) eventStream.Count;
        record.OverallPosition = (uint) _all.Count;
        _all.Add((streamId, eventStream.Count));
        eventStream.Add(record);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Load(Stream stream, CancellationToken cancellationToken = default)
    {
        var events = Serializer.DeserializeItems<EventRecord>(stream, PrefixStyle, 1);
        foreach (var e in events)
            Add(e);
    }

    #endregion
}