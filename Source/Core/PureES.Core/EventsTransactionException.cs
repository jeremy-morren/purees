namespace PureES.Core;

/// <summary>
/// Individual stream errors that occurred submitting a <see cref="EventsTransaction"/>
/// </summary>
public class EventsTransactionException : AggregateException
{
    public EventsTransactionException(IReadOnlyList<Exception> innerExceptions)
        : base(BuildMessage(innerExceptions), innerExceptions)
    {
    }

    private static string BuildMessage(IReadOnlyList<Exception> innerExceptions)
    {
        if (innerExceptions.Count < 2)
            throw new ArgumentOutOfRangeException(nameof(innerExceptions));

        var messages = innerExceptions
            .Select(e =>
            {
                return e switch
                {
                    StreamNotFoundException a => $"{a.StreamId}: {nameof(StreamNotFoundException)}",
                    WrongStreamRevisionException b => $"{b.StreamId}: {nameof(WrongStreamRevisionException)}",
                    StreamAlreadyExistsException c => $"{c.StreamId}: {nameof(StreamAlreadyExistsException)}",
                    _ => throw new NotImplementedException()
                };
            });
        return string.Join(". ", messages);
    }
}