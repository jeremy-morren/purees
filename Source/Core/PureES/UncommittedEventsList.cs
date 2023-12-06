namespace PureES;

[PublicAPI]
public class UncommittedEventsList
{
    public ulong? ExpectedRevision { get; }
    public List<UncommittedEvent> Events { get; }

    public UncommittedEventsList(ulong? expectedRevision, IEnumerable<object> @events)
    {
        ExpectedRevision = expectedRevision;
        Events = @events.Select(e => new UncommittedEvent(e)).ToList();
    }
}