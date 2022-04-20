using System.Collections.Immutable;

namespace PureES.Core;

public interface IEventStore
{
    Task<ulong> Create(string streamId,
        ImmutableArray<UncommittedEvent> events,
        CancellationToken cancellationToken);
    
    Task<ulong> Create(string streamId,
        UncommittedEvent @event,
        CancellationToken cancellationToken);

    Task<ulong> Append(string streamId,
        ulong expectedVersion,
        ImmutableArray<UncommittedEvent> events,
        CancellationToken cancellationToken);
    
    Task<ulong> Append(string streamId,
        ulong expectedVersion,
        UncommittedEvent @event,
        CancellationToken cancellationToken);

    Task<ImmutableArray<EventEnvelope>> Load(string streamId, 
        ulong? expectedVersion, 
        CancellationToken cancellationToken);
}