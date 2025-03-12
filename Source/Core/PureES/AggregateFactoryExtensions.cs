namespace PureES;

[PublicAPI]
public static class AggregateFactoryExtensions
{
    /// <summary>
    /// Creates a new aggregate from the given event stream
    /// </summary>
    public static Task<RehydratedAggregate<T>> Create<T>(this IAggregateFactory<T> factory, IEventStoreStream stream, CancellationToken ct)
    {
        return factory.Create(stream.StreamId, stream, ct);
    }
}