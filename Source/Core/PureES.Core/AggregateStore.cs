using PureES.Core.EventStore;
using PureES.Core.ExpBuilders.WhenHandlers;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

namespace PureES.Core;

public class AggregateStore<TAggregate> : IAggregateStore<TAggregate> where TAggregate : notnull
{
    protected readonly IEventStore EventStore;
    
    private readonly IServiceProvider _serviceProvider;
    private readonly AggregateFactory<TAggregate> _factory;

    public AggregateStore(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        EventStore = serviceProvider.GetRequiredService<IEventStore>();
        _factory = serviceProvider.GetRequiredService<PureESServices>()
                       .GetService<AggregateFactory<TAggregate>>()
                   ?? throw new InvalidOperationException($"Unable to get factory for aggregate {typeof(TAggregate)}");
    }

    protected virtual IAsyncEnumerable<EventEnvelope> Load(string streamId,
        CancellationToken cancellationToken) =>
        EventStore.Read(streamId, cancellationToken);

    protected virtual IAsyncEnumerable<EventEnvelope> Load(string streamId,
        ulong expectedRevision, CancellationToken cancellationToken) => 
        EventStore.Read(streamId, expectedRevision, cancellationToken);

    protected virtual IAsyncEnumerable<EventEnvelope> LoadPartial(string streamId,
        ulong requiredRevision, CancellationToken cancellationToken) =>
        EventStore.ReadPartial(streamId, requiredRevision, cancellationToken);

    ValueTask<TAggregate> IAggregateStore<TAggregate>.Create(IAsyncEnumerable<EventEnvelope> events, 
        CancellationToken cancellationToken) => _factory(events, _serviceProvider, cancellationToken);

    ValueTask<TAggregate> IAggregateStore<TAggregate>.Load(string streamId, 
        CancellationToken cancellationToken) => 
        _factory(Load(streamId, cancellationToken), _serviceProvider, cancellationToken);
    
    ValueTask<TAggregate> IAggregateStore<TAggregate>.Load(string streamId, 
        ulong expectedRevision, 
        CancellationToken cancellationToken) =>
        _factory(Load(streamId, expectedRevision, cancellationToken), _serviceProvider, cancellationToken);

    ValueTask<TAggregate> IAggregateStore<TAggregate>.LoadPartial(string streamId,
        ulong requiredRevision,
        CancellationToken cancellationToken) => 
        _factory(LoadPartial(streamId, requiredRevision, cancellationToken), _serviceProvider, cancellationToken);
}