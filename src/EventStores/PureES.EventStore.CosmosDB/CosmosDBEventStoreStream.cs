namespace PureES.EventStore.CosmosDB;

internal class CosmosDBEventStoreStream : IEventStoreStream
{
    private readonly IAsyncEnumerable<EventEnvelope> _events;

    public CosmosDBEventStoreStream(Direction direction, string streamId, IAsyncEnumerable<EventEnvelope> events)
    {
        _events = events;
        Direction = direction;
        StreamId = streamId;
    }

    public IAsyncEnumerator<EventEnvelope> GetAsyncEnumerator(CancellationToken cancellationToken) =>
        _events.GetAsyncEnumerator(cancellationToken);

    public string StreamId { get; }
    public Direction Direction { get; }
}