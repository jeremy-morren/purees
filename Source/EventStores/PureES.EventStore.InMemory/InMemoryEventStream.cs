using System.Collections;

namespace PureES.EventStore.InMemory;

internal class InMemoryEventStream : IEventStoreStream, IEnumerable<EventEnvelope>
{
    private readonly List<EventEnvelope> _events;

    public string StreamId { get; }
    public Direction Direction { get; }

    public InMemoryEventStream(Direction direction,
        string streamId,
        IEnumerable<InMemoryEventRecord> records,
        InMemoryEventStoreSerializer serializer)
    {
        StreamId = streamId;
        Direction = direction;
        _events = records.Select(serializer.Deserialize).ToList();
    }

    public IAsyncEnumerator<EventEnvelope> GetAsyncEnumerator(CancellationToken cancellationToken) =>
        _events.ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);

    public IEnumerator<EventEnvelope> GetEnumerator() => _events.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_events).GetEnumerator();
}