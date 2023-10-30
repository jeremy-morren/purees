using JetBrains.Annotations;
using PureES.Core;

namespace PureES.EventStore.InMemory;

[PublicAPI]
public interface IInMemoryEventStore : IEventStore
{
    IReadOnlyList<EventEnvelope> ReadAll();
}