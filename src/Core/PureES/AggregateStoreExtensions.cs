namespace PureES;

[PublicAPI]
public static class AggregateStoreExtensions
{
    extension<TAggregate>(IAggregateFactory<TAggregate> factory) where TAggregate : notnull
    {
        /// <summary>
        /// Rehydrates an aggregate from the given stream
        /// </summary>
        public ValueTask<TAggregate> RehydrateAggregate(
            IEventStoreStream stream,
            CancellationToken cancellationToken) =>
            factory.RehydrateAggregate(stream.StreamId, stream, cancellationToken);

        /// <summary>
        /// Rehydrates an aggregate from the given stream
        /// </summary>
        public async ValueTask<TAggregate> RehydrateAggregate(
            string streamId,
            IAsyncEnumerable<EventEnvelope> stream,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(stream);

            await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
            if (!await enumerator.MoveNextAsync())
                throw new StreamNotFoundException(streamId);
            var current = await factory.CreateWhen(enumerator.Current, cancellationToken);
            while (await enumerator.MoveNextAsync())
                current = await factory.UpdateWhen(enumerator.Current, current, cancellationToken);
            return current;
        }
    }
}