using JetBrains.Annotations;
using PureES.Core;

namespace PureES.EventStore.InMemory;

[PublicAPI]
public interface IInMemoryEventStore : IEventStore
{
    IReadOnlyList<EventEnvelope> ReadAll();

    /// <summary>
    /// Initializes the event store from the given envelopes.
    /// </summary>
    /// <param name="envelopes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">The event store already events</exception>
    Task Load(IAsyncEnumerable<EventEnvelope> envelopes, CancellationToken ct);
}