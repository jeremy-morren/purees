using PureES.Core;

namespace PureES.EventStore.InMemory;

public static class InMemoryEventStoreExtensions
{
    /// <summary>
    /// Loads all events from <see cref="source"/> into store
    /// </summary>
    /// <returns></returns>
    public static Task LoadFrom(this IInMemoryEventStore destination, IEventStore source, CancellationToken ct) => 
        destination.Load(source.ReadAll(ct), ct);
}