namespace PureES;

[PublicAPI]
public static class AggregateStoreExtensions
{
    /// <summary>
    /// Rehydrates an aggregate from the given stream
    /// </summary>
    public static async ValueTask<TAggregate> RehydrateAggregate<TAggregate>(
        this IAggregateFactory<TAggregate> factory,
        IEventStoreStream stream,
        CancellationToken cancellationToken)
        where TAggregate : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(stream);

        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync())
            throw new StreamNotFoundException(stream.StreamId);
        var current = await factory.CreateWhen(enumerator.Current, cancellationToken);
        while (await enumerator.MoveNextAsync())
            current = await factory.UpdateWhen(enumerator.Current, current, cancellationToken);
        return current;
    }
}