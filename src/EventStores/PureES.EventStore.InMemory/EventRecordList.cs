using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;

namespace PureES.EventStore.InMemory;

internal class EventRecordList : IReadOnlyList<InMemoryEventRecord>
{
    /// <summary>
    /// All event records
    /// </summary>
    private readonly ImmutableList<InMemoryEventRecord> _records;

    /// <summary>
    /// Index of stream IDs to list of event record indexes in _records
    /// </summary>
    private readonly ImmutableDictionary<string, ImmutableList<int>> _streams;

    /// <summary>
    /// Index of event types to list of event record indexes in _records
    /// </summary>
    // private readonly ImmutableDictionary<string, ImmutableList<int>> _eventTypes;

    private EventRecordList(
        ImmutableList<InMemoryEventRecord> records,
        ImmutableDictionary<string, ImmutableList<int>> streams)
    {
        _records = records;
        _streams = streams;
    }

    [Pure]
    public bool Exists(string streamId) => _streams.ContainsKey(streamId);

    [Pure]
    public bool TryGetRevision(string streamId, out uint revision)
    {
        if (_streams.TryGetValue(streamId, out var list))
        {
            revision = (uint)list.Count - 1;
            return true;
        }

        revision = uint.MaxValue;
        return false;
    }

    [Pure]
    public EventRecordList Append(string streamId, List<InMemoryEventRecord> events, out uint revision)
    {
        var stream = _streams.GetValueOrDefault(streamId) ?? ImmutableList<int>.Empty;

        for (var i = 0; i < events.Count; i++)
            events[i].StreamPos = stream.Count + i;

        stream = stream.AddRange(Enumerable.Range(_records.Count, events.Count));
        var records = _records.AddRange(events);
        revision = (uint)stream.Count - 1;

        return new EventRecordList(records, _streams.SetItem(streamId, stream));
    }

    public static readonly EventRecordList Empty = new(
        ImmutableList<InMemoryEventRecord>.Empty, ImmutableDictionary<string, ImmutableList<int>>.Empty);

    #region Read

    [Pure]
    public IEnumerable<InMemoryEventRecord> ReadAll(Direction direction)
    {
        return direction switch
        {
            Direction.Forwards => _records,
            Direction.Backwards => _records.Reverse(),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    [Pure]
    public IEnumerable<InMemoryEventRecord> ReadAll(Direction direction, uint maxCount) =>
        ReadAll(direction).Take((int)maxCount);

    /// <summary>
    /// Reads a stream of events
    /// </summary>
    public EventStreamReader ReadStream(string streamId, Direction direction)
    {
        if (!_streams.TryGetValue(streamId, out var indexes))
            throw new StreamNotFoundException(streamId);

        var revision = (uint)indexes.Count - 1;
        var read = new EventStreamReader(_records, indexes, revision);

        return direction switch
        {
            Direction.Forwards => read,
            Direction.Backwards => read.Reverse(),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    #endregion

    #region List Implementation

    public IEnumerator<InMemoryEventRecord> GetEnumerator() => _records.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_records).GetEnumerator();

    public int Count => _records.Count;

    public InMemoryEventRecord this[int index] => _records[index];

    #endregion
    
    #region Serialization

    public ImmutableList<InMemoryEventRecord> Records => _records;

    #endregion
}