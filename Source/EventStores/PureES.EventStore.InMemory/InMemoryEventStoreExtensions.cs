using JetBrains.Annotations;

namespace PureES.EventStore.InMemory;

[PublicAPI]
public static class InMemoryEventStoreExtensions
{
    /// <summary>
    /// Loads all events from <see cref="source"/> into store
    /// </summary>
    /// <returns></returns>
    public static Task LoadFrom(this IInMemoryEventStore destination, IEventStore source, CancellationToken ct) => 
        destination.Load(source.ReadAll(ct), ct);

    public static IEnumerable<EventEnvelope> ReadSync(this IInMemoryEventStore eventStore, string streamId) =>
        eventStore.ReadSync(Direction.Forwards, streamId);
}