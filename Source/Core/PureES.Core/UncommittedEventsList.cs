namespace PureES.Core;

[PublicAPI]
public class UncommittedEventsList
{
    public ulong? ExpectedRevision { get; }
    public List<UncommittedEvent> Events { get; }

    public UncommittedEventsList(EventsList source)
    {
        ExpectedRevision = source.ExpectedRevision;
        Events = source.Select(e => new UncommittedEvent(e)).ToList();
    }
}