using System.Collections.Immutable;
using System.Diagnostics.Contracts;

namespace PureES.EventStore.InMemory;

/// <summary>
/// A reader for a stream of events
/// </summary>
internal class EventStreamReader
{
    /// <summary>
    /// All event records in the event store
    /// </summary>
    private readonly ImmutableList<InMemoryEventRecord> _eventStore;
    private readonly IReadOnlyList<int> _stream;

    /// <summary>
    /// Actual stream revision
    /// </summary>
    public uint ActualRevision { get; }

    public EventStreamReader(
        ImmutableList<InMemoryEventRecord> eventStore,
        IReadOnlyList<int> stream,
        uint actualRevision)
    {
        _eventStore = eventStore;
        _stream = stream;

        ActualRevision = actualRevision;
    }

    public IEnumerable<EventEnvelope> GetEvents(InMemoryEventStoreSerializer serializer) =>
        _stream.Select(i => _eventStore[i]).Select(serializer.Deserialize);

    /// <summary>
    /// Reverses the stream enumeration
    /// </summary>
    [Pure]
    public EventStreamReader Reverse() => Clone(_stream.Reverse());

    /// <summary>
    /// Skips the given number of events in the stream
    /// </summary>
    [Pure]
    public EventStreamReader Skip(int count) => Clone(_stream.Skip(count));

    /// <summary>
    /// Skips the given number of events at the end of the stream
    /// </summary>
    [Pure]
    public EventStreamReader SkipLast(int count) => Clone(_stream.SkipLast(count).ToList());

    /// <summary>
    /// Takes the given number of events from the start of the stream
    /// </summary>
    [Pure]
    public EventStreamReader Take(int count) => Clone(_stream.Take(count));

    [Pure]
    private EventStreamReader Clone(IEnumerable<int> stream) =>
        new(_eventStore, stream.ToList(), ActualRevision);
}