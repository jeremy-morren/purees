namespace PureES;

[PublicAPI]
public static class AggregateStoreExtensions
{
    /// <summary>
    /// Rehydrates an aggregate from the stream using the given factory
    /// </summary>
    public static async ValueTask<TAggregate> RehydrateAggregate<TAggregate>(
        this IAggregateStore<TAggregate> store,
        IEventStoreStream stream,
        IAggregateFactory<TAggregate> factory,
        CancellationToken cancellationToken)
        where TAggregate : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(factory);

        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync())
            throw new StreamNotFoundException(stream.StreamId);
        var current = await factory.CreateWhen(enumerator.Current, cancellationToken);
        while (await enumerator.MoveNextAsync())
            current = await factory.UpdateWhen(enumerator.Current, current, cancellationToken);
        return current;
    }
}