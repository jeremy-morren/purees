using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStores.Tests;

public sealed class EventStoreTestHarness : IEventStore, IAsyncDisposable
{
    private readonly IAsyncDisposable _harness;
    private readonly IEventStore _eventEventStore;

    public EventStoreTestHarness(IAsyncDisposable harness, IEventStore eventStore)
    {
        _harness = harness;
        _eventEventStore = eventStore;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    #region Implementation of IEventStore

    public Task<bool> Exists(string streamId, CancellationToken cancellationToken) => _eventEventStore.Exists(streamId, cancellationToken);

    public Task<ulong> GetRevision(string streamId, CancellationToken cancellationToken) => _eventEventStore.GetRevision(streamId, cancellationToken);

    public Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken cancellationToken) => _eventEventStore.GetRevision(streamId, expectedRevision, cancellationToken);

    public Task<ulong> Create(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken) => _eventEventStore.Create(streamId, events, cancellationToken);

    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken) => _eventEventStore.Create(streamId, @event, cancellationToken);

    public Task<ulong> Append(string streamId, ulong expectedRevision, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken) => _eventEventStore.Append(streamId, expectedRevision, events, cancellationToken);

    public Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken) => _eventEventStore.Append(streamId, events, cancellationToken);

    public Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event, CancellationToken cancellationToken) => _eventEventStore.Append(streamId, expectedRevision, @event, cancellationToken);

    public Task<ulong> Append(string streamId, UncommittedEvent @event, CancellationToken cancellationToken) => _eventEventStore.Append(streamId, @event, cancellationToken);

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, CancellationToken cancellationToken = default) => _eventEventStore.ReadAll(direction, cancellationToken);

    public IAsyncEnumerable<EventEnvelope> ReadAll(Direction direction, ulong maxCount, CancellationToken cancellationToken = default) => _eventEventStore.ReadAll(direction, maxCount, cancellationToken);

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, CancellationToken cancellationToken = default) => _eventEventStore.Read(direction, streamId, cancellationToken);

    public IAsyncEnumerable<EventEnvelope> Read(Direction direction, string streamId, ulong expectedRevision,
        CancellationToken cancellationToken = default) =>
        _eventEventStore.Read(direction, streamId, expectedRevision, cancellationToken);

    public IAsyncEnumerable<EventEnvelope> ReadPartial(Direction direction, string streamId, ulong requiredRevision,
        CancellationToken cancellationToken = default) =>
        _eventEventStore.ReadPartial(direction, streamId, requiredRevision, cancellationToken);

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, IEnumerable<string> streams, CancellationToken cancellationToken = default) => _eventEventStore.ReadMany(direction, streams, cancellationToken);

    public IAsyncEnumerable<IAsyncEnumerable<EventEnvelope>> ReadMany(Direction direction, IAsyncEnumerable<string> streams,
        CancellationToken cancellationToken = default) =>
        _eventEventStore.ReadMany(direction, streams, cancellationToken);

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, Type eventType, CancellationToken cancellationToken = default) => _eventEventStore.ReadByEventType(direction, eventType, cancellationToken);

    public IAsyncEnumerable<EventEnvelope> ReadByEventType(Direction direction, Type eventType, ulong maxCount,
        CancellationToken cancellationToken = default) =>
        _eventEventStore.ReadByEventType(direction, eventType, maxCount, cancellationToken);

    public Task<ulong> Count(CancellationToken cancellationToken) => _eventEventStore.Count(cancellationToken);

    public Task<ulong> CountByEventType(Type eventType, CancellationToken cancellationToken) => _eventEventStore.CountByEventType(eventType, cancellationToken);
    
    #endregion
}