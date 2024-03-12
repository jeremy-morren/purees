namespace PureES;

/// <summary>
/// A rehydrated aggregate
/// </summary>
/// <param name="Aggregate">The Aggregate</param>
/// <param name="StreamPosition">The stream position of the aggregate</param>
/// <typeparam name="TAggregate">The Aggregate</typeparam>
[PublicAPI]
public record RehydratedAggregate<TAggregate>(TAggregate Aggregate, ulong StreamPosition);