using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStore.InMemory;

public interface IInMemoryEventStore : IEventStore
{
    IReadOnlyList<EventEnvelope> ReadAll();

    void Save(Stream stream, CancellationToken cancellationToken = default);
    void Load(Stream stream, CancellationToken cancellationToken = default);
}