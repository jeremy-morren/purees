namespace PureES.EventStores.Tests;

public static class EventsTransactionHelpers
{
    /// <summary>
    /// Creates an uncommitted events transaction that can be submitted to the event store.
    /// </summary>
    /// <param name="transaction">The source transaction</param>
    /// <returns>An uncommitted events transaction that can be submitted to the event store</returns>
    public static IReadOnlyList<UncommittedEventsList> ToUncommittedTransaction(this IEventsTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return transaction.Select(
                p => new UncommittedEventsList(p.Key, p.Value.ExpectedRevision, p.Value))
            .ToList();
    }
}