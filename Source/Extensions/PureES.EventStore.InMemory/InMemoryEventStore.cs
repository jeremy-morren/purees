using System.Collections.Immutable;
using EventStore.Client;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.EventStoreDB;
using PureES.EventStoreDB.Serialization;
using StreamNotFoundException = PureES.Core.EventStore.StreamNotFoundException;

namespace PureES.EventStore.InMemory;

public class InMemoryEventStore : IEventStore
{
    private readonly List<(string StreamId, int StreamIndex)> _all = new();

    private readonly Dictionary<string, List<(DateTime Created, EventData Event)>> _events = new();
    private readonly object _mutex = new();

    private readonly IEventStoreDBSerializer _serializer;

    public InMemoryEventStore(IEventStoreDBSerializer serializer) => _serializer = serializer;

    public Task<ulong> GetRevision(string streamId, CancellationToken _)
    {
        lock (_mutex)
        {
            if (!_events.TryGetValue(streamId, out var events))
                throw new StreamNotFoundException(streamId);
            return Task.FromResult((ulong) events.Count - 1);
        }
    }

    public Task<bool> Exists(string streamId, CancellationToken _)
    {
        lock (_mutex)
        {
            return Task.FromResult(_events.ContainsKey(streamId));
        }
    }

    public Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken _)
    {
        lock (_mutex)
        {
            if (_events.TryGetValue(streamId, out var current))
                throw new StreamAlreadyExistsException(streamId, (ulong)current.Count - 1);
            _events.Add(streamId, new List<(DateTime Created, EventData Event)>());
            var timestamp = DateTime.UtcNow;
            var values = events
                .Select(e => (timestamp, _serializer.Serialize(e)))
                .ToList();
            return Task.FromResult(AppendStream(streamId, values));
        }
    }

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken _)
        => Create(streamId, ImmutableArray.Create(@event), _);

    public Task<ulong> Append(string streamId, ulong expectedVersion, IEnumerable<UncommittedEvent> events,
        CancellationToken _)
    {
        lock (_mutex)
        {
            if (!_events.TryGetValue(streamId, out var current))
                throw new StreamNotFoundException(streamId);
            if (current.Count - 1 != (long) expectedVersion)
                throw new WrongStreamVersionException(streamId,
                    expectedVersion,
                    (ulong)current.Count - 1);
            var timestamp = DateTime.UtcNow;
            var values = events
                .Select(e => (timestamp, _serializer.Serialize(e)))
                .ToList();
            return Task.FromResult(AppendStream(streamId, values));
        }
    }

    public Task<ulong> Append(string streamId, ulong expectedVersion, UncommittedEvent @event, CancellationToken _)
        => Append(streamId, expectedVersion, ImmutableArray.Create(@event), _);

    public IAsyncEnumerable<EventEnvelope> Load(string streamId, CancellationToken _)
    {
        lock (_mutex)
        {
            if (!_events.TryGetValue(streamId, out var events))
                throw new StreamNotFoundException(streamId);
            var list = Enumerable.Range(0, events.Count)
                .Select(i => ToEventRecord(streamId, i, events[i].Created, events[i].Event))
                .Select(_serializer.Deserialize)
                .ToList();

            return list.ToAsyncEnumerable();
        }
    }

    public IAsyncEnumerable<EventEnvelope> Load(string streamId, ulong expectedRevision, CancellationToken _)
    {
        lock (_mutex)
        {
            if (!_events.TryGetValue(streamId, out var events))
                throw new StreamNotFoundException(streamId);
            var length = (ulong) events.Count - 1;
            if (length != expectedRevision)
                throw new WrongStreamVersionException(streamId,
                    new StreamRevision(expectedRevision),
                    new StreamRevision(length));
            var list = Enumerable.Range(0, events.Count)
                .Select(i => ToEventRecord(streamId, i, events[i].Created, events[i].Event))
                .Select(_serializer.Deserialize)
                .ToList();

            return list.ToAsyncEnumerable();
        }
    }

    public IAsyncEnumerable<EventEnvelope> LoadPartial(string streamId, ulong requiredRevision, CancellationToken _)
    {
        lock (_mutex)
        {
            if (!_events.TryGetValue(streamId, out var events))
                throw new StreamNotFoundException(streamId);
            var length = (ulong) events.Count - 1;
            if (length < requiredRevision)
                throw new WrongStreamVersionException(streamId,
                    new StreamRevision(requiredRevision),
                    new StreamRevision(length));
            var list = Enumerable.Range(0, (int) requiredRevision + 1)
                .Select(i => ToEventRecord(streamId, i, events[i].Created, events[i].Event))
                .Select(_serializer.Deserialize)
                .ToList();

            return list.ToAsyncEnumerable();
        }
    }

    public IAsyncEnumerable<EventEnvelope> LoadByEventType(Type eventType, CancellationToken cancellationToken)
    {
        var name = _serializer.GetTypeName(eventType);
        lock (_mutex)
        {
            var list = new List<EventEnvelope>();
            foreach (var (streamId, streamIndex) in _all)
            {
                var record = _events[streamId][streamIndex];
                if (record.Event.Type != name)
                    continue;
                list.Add(_serializer.Deserialize(ToEventRecord(streamId, streamIndex, record.Created, record.Event)));
            }
            return list.ToAsyncEnumerable();
        }
    }

    public IReadOnlyList<EventEnvelope> GetAll()
    {
        lock (_mutex)
        {
            var list = new List<EventEnvelope>();
            foreach (var (streamId, streamIndex) in _all)
            {
                var stream = _events[streamId];
                var record = ToEventRecord(streamId, streamIndex, stream[streamIndex].Created,
                    stream[streamIndex].Event);
                list.Add(_serializer.Deserialize(record));
            }

            return list;
        }
    }

    private ulong AppendStream(string streamId, IReadOnlyList<(DateTime Created, EventData Event)> events)
    {
        var current = _events[streamId];
        var startIndex = current.Count;
        for (var i = 0; i < events.Count; i++)
        {
            current.Add(events[i]);
            _all.Add((streamId, i + startIndex));
        }

        return (ulong) (startIndex + events.Count) - 1;
    }

    private static EventRecord ToEventRecord(string streamId, long position, DateTime created, EventData @event)
    {
        //In EventRecord constructor
        //EventType = metadata["type"];
        //Created = Convert.ToInt64(metadata["created"]).FromTicksSinceEpoch();
        //ContentType = metadata["content-type"];
        var metadata = new Dictionary<string, string>
        {
            {"created", created.Ticks.ToString()},
            {"type", @event.Type},
            {"content-type", "application/json"}
        };
        return new EventRecord(streamId,
            @event.EventId,
            StreamPosition.FromInt64(position),
            new Position(ulong.MaxValue, ulong.MaxValue),
            metadata,
            @event.Data,
            @event.Metadata);
    }
}