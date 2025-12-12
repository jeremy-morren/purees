namespace PureES.EventStore.EFCore;

internal class EfCoreEventStoreStream : IEventStoreStream
{
    private readonly IAsyncEnumerable<EventEnvelope> _events;
    public string StreamId { get; }
    public Direction Direction { get; }

    public EfCoreEventStoreStream(Direction direction, string streamId, IAsyncEnumerable<EventEnvelope> events)
    {
        _events = events;
        StreamId = streamId;
        Direction = direction;
    }

    public IAsyncEnumerator<EventEnvelope> GetAsyncEnumerator(CancellationToken cancellationToken) =>
        _events.GetAsyncEnumerator(cancellationToken);
}