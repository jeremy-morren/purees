using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStore.InMemory;

public interface IInMemoryEventStore : IEventStore
{
    IReadOnlyList<EventEnvelope> ReadAll();
    IReadOnlyList<EventEnvelope> ReadByEventType(Type eventType);

    void Save(Stream stream, CancellationToken cancellationToken = default);
    void Load(Stream stream, CancellationToken cancellationToken = default);
}