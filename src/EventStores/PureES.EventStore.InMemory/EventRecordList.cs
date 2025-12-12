using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;

namespace PureES.EventStore.InMemory;

internal class EventRecordList : IReadOnlyList<InMemoryEventRecord>
{
    private readonly ImmutableList<InMemoryEventRecord> _records;
    private readonly ImmutableDictionary<string, ImmutableList<int>> _streams;

    private EventRecordList(ImmutableList<InMemoryEventRecord> records,
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

    public IEnumerable<InMemoryEventRecord> ReadStream(Direction direction, string streamId, out uint revision)
    {
        if (!_streams.TryGetValue(streamId, out var stream))
            throw new StreamNotFoundException(streamId);
        revision = (uint)stream.Count - 1;
        var indexes = direction switch
        {
            Direction.Forwards => stream,
            Direction.Backwards => stream.Reverse(),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        return indexes.Select(i => _records[i]);
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