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
    
    /// <summary>
    /// Indexes of the events we are reading in the stream
    /// </summary>
    private readonly IEnumerable<int> _stream;

    /// <summary>
    /// Actual stream revision
    /// </summary>
    public uint ActualRevision { get; }

    public EventStreamReader(
        ImmutableList<InMemoryEventRecord> eventStore,
        IEnumerable<int> stream,
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
    public EventStreamReader Skip(uint count)
    {
        if (count == 0)
            return this;
        
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (uint)int.MaxValue);
        return Clone(_stream.Skip((int)count));
    }

    /// <summary>
    /// Skips the given number of events at the end of the stream
    /// </summary>
    [Pure]
    public EventStreamReader SkipLast(uint count)
    {
        if (count == 0)
            return this;
        
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (uint)int.MaxValue);
        return Clone(_stream.SkipLast((int)count));
    }

    /// <summary>
    /// Takes the given number of events from the start of the stream
    /// </summary>
    [Pure]
    public EventStreamReader Take(uint count)
    {
        if (count == 0)
            return this;
        
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (uint)int.MaxValue);
        return Clone(_stream.Take((int)count));
    }

    [Pure]
    private EventStreamReader Clone(IEnumerable<int> stream) =>
        new(_eventStore, stream, ActualRevision);
}