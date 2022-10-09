using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using EventStore.Client;
using Microsoft.Extensions.Internal;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.EventStoreDB;
using PureES.EventStoreDB.Serialization;
using StreamNotFoundException = PureES.Core.EventStore.StreamNotFoundException;

// ReSharper disable MemberCanBeProtected.Global

namespace PureES.EventStore.InMemory;

public class InMemoryEventStore : IEventStore
{
    private readonly List<(string StreamId, int StreamIndex)> _all = new();
    private readonly Dictionary<string, List<EventEntry>> _events = new();

    private readonly IEventStoreDBSerializer _serializer;
    private readonly ISystemClock _systemClock;

    public InMemoryEventStore(IEventStoreDBSerializer serializer,
        ISystemClock systemClock)
    {
        _serializer = serializer;
        _systemClock = systemClock;
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
        _events.Add(streamId, new List<EventEntry>());
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
    public IAsyncEnumerable<EventEnvelope> Load(string streamId, CancellationToken _)
    {
        if (!_events.TryGetValue(streamId, out var events))
            throw new StreamNotFoundException(streamId);
        var list = Enumerable.Range(0, events.Count)
            .Select(i => events[i].ToEventRecord(i))
            .Select(_serializer.Deserialize)
            .ToList();

        return list.ToAsyncEnumerable();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IAsyncEnumerable<EventEnvelope> Load(string streamId, ulong expectedRevision, CancellationToken _)
    {
        if (!_events.TryGetValue(streamId, out var events))
            throw new StreamNotFoundException(streamId);
        var length = (ulong) events.Count - 1;
        if (length != expectedRevision)
            throw new WrongStreamRevisionException(streamId,
                new StreamRevision(expectedRevision),
                new StreamRevision(length));
        var list = Enumerable.Range(0, events.Count)
            .Select(i => events[i].ToEventRecord(i))
            .Select(_serializer.Deserialize)
            .ToList();

        return list.ToAsyncEnumerable();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IAsyncEnumerable<EventEnvelope> LoadPartial(string streamId, ulong requiredRevision, CancellationToken _)
    {
        if (!_events.TryGetValue(streamId, out var events))
            throw new StreamNotFoundException(streamId);
        var length = (ulong) events.Count - 1;
        if (length < requiredRevision)
            throw new WrongStreamRevisionException(streamId,
                requiredRevision,
                length);
        var list = Enumerable.Range(0, (int) requiredRevision + 1)
            .Select(i => events[i].ToEventRecord(i))
            .Select(_serializer.Deserialize)
            .ToList();

        return list.ToAsyncEnumerable();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IAsyncEnumerable<EventEnvelope> LoadByEventType(Type eventType, CancellationToken cancellationToken)
    {
        var name = _serializer.GetTypeName(eventType);
        var list = new List<EventEnvelope>();
        foreach (var (streamId, streamIndex) in _all)
        {
            var entry = _events[streamId][streamIndex];
            if (entry.Type != name)
                continue;
            var env = _serializer.Deserialize(entry.ToEventRecord(streamIndex));
            list.Add(env);
        }
        return list.ToAsyncEnumerable();
    }

    private ulong AppendStream(string streamId, IEnumerable<UncommittedEvent> events)
    {
        var timestamp = _systemClock.UtcNow;
        var values = events
            .Select(e => EventEntry.FromEventData(streamId, timestamp, _serializer.Serialize(e)))
            .ToList();
        var current = _events[streamId];
        var startIndex = current.Count;
        for (var i = 0; i < values.Count; i++)
        {
            current.Add(values[i]);
            _all.Add((streamId, i + startIndex));
        }
        return (ulong) (startIndex + values.Count) - 1;
    }

    #endregion

    #region Testing

    //These methods are intended to facilitate unit tests

    [MethodImpl(MethodImplOptions.Synchronized)]
    public IReadOnlyList<EventEnvelope> GetAll()
    {
        var list = new List<EventEnvelope>();
        foreach (var (streamId, streamIndex) in _all)
        {
            var stream = _events[streamId];
            var record = stream[streamIndex].ToEventRecord(streamIndex);
            list.Add(_serializer.Deserialize(record));
        }
        return list;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void Save(Utf8JsonWriter writer, CancellationToken cancellationToken)
    {
        writer.WriteStartArray();
        foreach (var (streamId, streamIndex) in _all)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = _events[streamId][streamIndex];
            JsonSerializer.Serialize(writer, record);
        }
        writer.WriteEndArray();
    }
    
    public void Save(Stream stream, CancellationToken cancellationToken = default)
    {
        using var writer = new Utf8JsonWriter(stream);
        Save(writer, cancellationToken);
    }
    
    public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await using var writer = new Utf8JsonWriter(stream);
        Save(writer, cancellationToken);
    }
    
    [MethodImpl(MethodImplOptions.Synchronized)]
    private void Add(EventEntry @event)
    {
        var streamId = @event.StreamId;
        if (!_events.TryGetValue(streamId, out var eventStream))
        {
            eventStream = new List<EventEntry>();
            _events.Add(streamId, eventStream);
        }
        _all.Add((streamId, eventStream.Count));
        eventStream.Add(@event);
    }
    
    public void Load(Stream stream, CancellationToken cancellationToken = default)
    {
        var events = JsonSerializer.Deserialize<IEnumerable<EventEntry>>(stream) ?? Array.Empty<EventEntry>();
        foreach (var e in events)
            Add(e);
    }
    
    public async Task LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var events =
            JsonSerializer.DeserializeAsyncEnumerable<EventEntry>(stream, cancellationToken: cancellationToken);
        await foreach (var e in events.WithCancellation(cancellationToken))
            if (e != null)
                Add(e);
    }

    #endregion
    
    
    private record EventEntry(string StreamId, 
        DateTime Created, 
        Guid EventId,
        string Type,
        byte[] Data,
        byte[] Metadata)
    {
        public static EventEntry FromEventData(string streamId, DateTimeOffset created, EventData eventData)
            => new(streamId,
                created.UtcDateTime,
                eventData.EventId.ToGuid(),
                eventData.Type,
                eventData.Data.ToArray(),
                eventData.Metadata.ToArray());
        
        public EventRecord ToEventRecord(int position)
        {
            //In EventRecord constructor
            //EventType = metadata["type"];
            //Created = Convert.ToInt64(metadata["created"]).FromTicksSinceEpoch();
            //ContentType = metadata["content-type"];
            var metadata = new Dictionary<string, string>
            {
                {"created", Created.Ticks.ToString()},
                {"type", Type},
                {"content-type", "application/json"}
            };
            return new EventRecord(StreamId,
                Uuid.FromGuid(EventId),
                (ulong)position,
                new Position(ulong.MaxValue, ulong.MaxValue),
                metadata,
                Data, 
                Metadata);
        }
    }
}