using System.Collections;

namespace PureES.EventStore.InMemory;

/// <summary>
/// Implementation of <see cref="IEventStoreStream"/> for an in-memory event store
/// </summary>
internal class InMemoryEventStream : IEventStoreStream, IEnumerable<EventEnvelope>
{
    private readonly EventStreamReader _stream;
    private readonly InMemoryEventStoreSerializer _serializer;

    public InMemoryEventStream(
        Direction direction,
        string streamId,
        EventStreamReader stream,
        InMemoryEventStoreSerializer serializer)
    {
        StreamId = streamId;
        Direction = direction;

        _stream = stream;
        _serializer = serializer;
    }

    public string StreamId { get; }
    public Direction Direction { get; }

    private IEnumerable<EventEnvelope> GetEvents() => _stream.GetEvents(_serializer);

    public IAsyncEnumerator<EventEnvelope> GetAsyncEnumerator(CancellationToken cancellationToken) =>
        GetEvents().ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);

    public IEnumerator<EventEnvelope> GetEnumerator() => GetEvents().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)GetEvents()).GetEnumerator();
}