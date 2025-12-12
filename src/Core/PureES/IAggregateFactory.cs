namespace PureES;

/// <summary>
/// A factory for creating and updating aggregates
/// </summary>
/// <typeparam name="TAggregate">The aggregate type</typeparam>
[PublicAPI]
public interface IAggregateFactory<TAggregate>
    where TAggregate : notnull
{
    /// <summary>
    /// Creates a new aggregate from the given start event
    /// </summary>
    ValueTask<TAggregate> CreateWhen(EventEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the event to the aggregate, returning the updated aggregate
    /// </summary>
    ValueTask<TAggregate> UpdateWhen(EventEnvelope envelope, TAggregate current, CancellationToken cancellationToken);
}