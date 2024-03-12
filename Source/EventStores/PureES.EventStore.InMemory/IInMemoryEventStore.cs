using System.Text.Json;
using JetBrains.Annotations;

namespace PureES.EventStore.InMemory;

[PublicAPI]
public interface IInMemoryEventStore : IEventStore
{
    /// <summary>
    /// Gets the number of events in the store
    /// </summary>
    /// <returns></returns>
    int GetCount();
    
    /// <summary>
    /// Reads all events as a synchronous operation
    /// </summary>
    /// <returns></returns>
    IEnumerable<EventEnvelope> ReadAll();

    /// <summary>
    /// Initializes the event store from the given envelopes.
    /// </summary>
    /// <param name="envelopes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">The event store already events</exception>
    Task Load(IAsyncEnumerable<EventEnvelope> envelopes, CancellationToken ct);

    public IReadOnlyList<JsonElement> Serialize();
    public void Deserialize(IEnumerable<JsonElement> events);
}