using System.Collections;

namespace PureES;

/// <summary>
/// A list of uncommitted events for a stream
/// </summary>
[PublicAPI]
public class UncommittedEventsList : IReadOnlyList<UncommittedEvent>
{
    /// <summary>
    /// Stream id
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// Expected stream revision
    /// </summary>
    public uint? ExpectedRevision { get; }

    /// <summary>
    /// Events to be committed
    /// </summary>
    public IReadOnlyList<UncommittedEvent> Events { get; }

    public UncommittedEventsList(string streamId, uint? expectedRevision, IEnumerable<UncommittedEvent> events)
    {
        StreamId = streamId;
        ExpectedRevision = expectedRevision;
        Events = events.ToList();
    }

    public UncommittedEventsList(string streamId, uint? expectedRevision, IEnumerable<object> events)
        : this(streamId, expectedRevision, events.Select(e => new UncommittedEvent(e)))
    {
    }

    #region Implementation of IReadOnlyList<UncommittedEvent>

    public IEnumerator<UncommittedEvent> GetEnumerator()
    {
        return Events.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Events).GetEnumerator();
    }

    public int Count => Events.Count;

    public UncommittedEvent this[int index] => Events[index];

    #endregion

    public void Deconstruct(out string streamId, out uint? expectedRevision, out IReadOnlyList<UncommittedEvent> events)
    {
        streamId = StreamId;
        expectedRevision = ExpectedRevision;
        events = Events;
    }
}