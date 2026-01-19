using System.Collections.Immutable;
using JetBrains.Annotations;

namespace PureES.EventStore.InMemory;

/// <summary>
/// An event store that keeps events in memory
/// </summary>
[PublicAPI]
public interface IInMemoryEventStore : IEventStore
{
    /// <summary>
    /// Gets the number of events in the store
    /// </summary>
    /// <returns></returns>
    uint GetCountSync();
    
    /// <summary>
    /// Reads all events as a synchronous operation
    /// </summary>
    /// <returns></returns>
    IEnumerable<EventEnvelope> ReadAllSync();

    /// <summary>
    /// Reads stream events as a synchronous operation
    /// </summary>
    IEnumerable<EventEnvelope> ReadSync(Direction direction, string streamId);
    
    /// <summary>
    /// Checks if a stream exists as a synchronous operation
    /// </summary>
    bool ExistsSync(string streamId);

    /// <summary>
    /// Reads events by type as a synchronous operation
    /// </summary>
    IEnumerable<EventEnvelope> ReadByEventTypeSync(Direction direction, Type[] eventTypes);

    /// <summary>
    /// Counts the number of events by type as a synchronous operation
    /// </summary>
    uint CountByEventTypeSync(Type[] eventTypes);

    #region Load & Save

    /// <summary>
    /// Initializes the event store from the given envelopes.
    /// </summary>
    /// <param name="events"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">The event store already events</exception>
    Task Load(IAsyncEnumerable<EventEnvelope> @events, CancellationToken ct);

    /// <summary>
    /// Initializes the event store from the given records.
    /// </summary>
    /// <param name="events"></param>
    /// <param name="ct"></param>
    /// <exception cref="InvalidOperationException">The event store already events</exception>
    Task Load(IAsyncEnumerable<SerializedInMemoryEventRecord> events, CancellationToken ct);

    /// <summary>
    /// Initializes the event store from the given records.
    /// </summary>
    public void Load(IEnumerable<SerializedInMemoryEventRecord> events);

    /// <summary>
    /// Serializes the event store to a list of records.
    /// </summary>
    public IEnumerable<SerializedInMemoryEventRecord> Serialize();

    #endregion
}