namespace PureES.EventStores.Tests;

public static class EventsTransactionHelpers
{
    public static IReadOnlyList<UncommittedEventsList> ToUncommittedTransaction(
        this EventsTransaction transaction)
    {
        return transaction.Select(
                p => new UncommittedEventsList(p.Key, p.Value.ExpectedRevision, p.Value))
            .ToList();
    }
}