using System.Collections.Immutable;

namespace PureES.Core;

public interface IEventStore
{
    Task<bool> Exists(string streamId, CancellationToken cancellationToken);
    
    Task<ulong> Create(string streamId,
        IEnumerable<UncommittedEvent> events,
        CancellationToken cancellationToken);
    
    Task<ulong> Create(string streamId,
        UncommittedEvent @event,
        CancellationToken cancellationToken);

    Task<ulong> Append(string streamId,
        ulong expectedRevision,
        IEnumerable<UncommittedEvent> events,
        CancellationToken cancellationToken);
    
    Task<ulong> Append(string streamId,
        ulong expectedRevision,
        UncommittedEvent @event,
        CancellationToken cancellationToken);

    IAsyncEnumerable<EventEnvelope> Load(string streamId, 
        CancellationToken cancellationToken);
    
    IAsyncEnumerable<EventEnvelope> Load(string streamId, 
        ulong expectedRevision, 
        CancellationToken cancellationToken);
    
    IAsyncEnumerable<EventEnvelope> LoadPartial(string streamId, 
        ulong requiredRevision, 
        CancellationToken cancellationToken);

    Task<ulong> Count(string streamId, CancellationToken cancellationToken);

    IAsyncEnumerable<EventEnvelope> LoadByEventType(Type eventType, CancellationToken cancellationToken);
}