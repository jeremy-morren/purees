namespace PureES;

[PublicAPI]
public class UncommittedEventsList
{
    public uint? ExpectedRevision { get; }
    public List<UncommittedEvent> Events { get; }

    public UncommittedEventsList(uint? expectedRevision, IEnumerable<object> @events)
    {
        ExpectedRevision = expectedRevision;
        Events = @events.Select(e => new UncommittedEvent(e)).ToList();
    }
}