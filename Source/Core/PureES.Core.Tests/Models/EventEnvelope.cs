using JetBrains.Annotations;

namespace PureES.Core.Tests.Models;

[UsedImplicitly]
public class EventEnvelope<TEvent> : EventEnvelope<TEvent, Metadata> where TEvent : notnull
{
    public EventEnvelope(EventEnvelope source) : base(source) {}
}