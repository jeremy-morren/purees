using System.Collections;
using System.Linq.Expressions;
using PureES.Core.EventStore;

// ReSharper disable MemberCanBeMadeStatic.Global

namespace PureES.Core.ExpBuilders;

internal class InvokeEventStoreExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public InvokeEventStoreExpBuilder(CommandHandlerOptions options) => _options = options;

    //TODO Check StreamID null

    private static void ValidateParameters(Expression eventStore,
        Expression streamId,
        Expression cancellationToken)
    {
        //The type could be a proxy mock
        if (!typeof(IEventStore).IsAssignableFrom(eventStore.Type))
            throw new InvalidOperationException("Invalid EventStore expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new InvalidOperationException("Invalid CancellationToken expression");
        if (streamId.Type != typeof(string))
            throw new InvalidOperationException("Invalid StreamId expression");
    }

    #region Create
    
    public Expression CreateSingle(Expression eventStore,
        Expression streamId,
        Expression @event,
        Expression cancellationToken)
    {
        ValidateParameters(eventStore, streamId, cancellationToken);
        //Looks like eventStore.Create(streamId, event, cancellationToken)
        var method = typeof(IEventStore)
                         .GetMethods()
                         .SingleOrDefault(m => 
                             m.Name == nameof(IEventStore.Create)
                             && m.GetParameters().Length == 3 
                             && m.GetParameters()[1].ParameterType == typeof(UncommittedEvent)) 
                     ?? throw new InvalidOperationException("Unable to locate method IEventStore.Create(streamId, event, cancellationToken)");
        return Expression.Call(eventStore, method, streamId, @event, cancellationToken);
    }
    
    public Expression CreateMultiple(Expression eventStore,
        Expression streamId,
        Expression events,
        Expression cancellationToken)
    {
        ValidateParameters(eventStore, streamId, cancellationToken);
        //Looks like eventStore.Create(streamId, events, cancellationToken)
        var method = typeof(IEventStore)
                         .GetMethods()
                         .SingleOrDefault(m => 
                             m.Name == nameof(IEventStore.Create)
                             && m.GetParameters().Length == 3 
                             && typeof(IEnumerable).IsAssignableFrom(m.GetParameters()[1].ParameterType)) 
                     ?? throw new InvalidOperationException("Unable to locate method IEventStore.Create(streamId, events, cancellationToken)");
        return Expression.Call(eventStore, method, streamId, events, cancellationToken);
    }
    
    #endregion
    
    #region Append

    public Expression AppendSingle(Expression eventStore,
        Expression streamId, 
        Expression expectedVersion, 
        Expression @event,
        Expression cancellationToken)
    {
        ValidateParameters(eventStore, streamId, cancellationToken);
        
        //Looks like eventStore.Append(streamId, expectedVersion, uncommittedEvent, cancellationToken)
        var method = typeof(IEventStore)
                         .GetMethods()
                         .SingleOrDefault(m =>
                             m.Name == nameof(IEventStore.Append)
                             && m.GetParameters().Length == 4 
                             && m.GetParameters()[2].ParameterType == typeof(UncommittedEvent)) 
                     ?? throw new InvalidOperationException("Unable to locate method IEventStore.Append(streamId, expectedVersion, event, cancellationToken)");
        return Expression.Call(eventStore, method, streamId, expectedVersion, @event, cancellationToken);
    }
    
    public Expression AppendMultiple(Expression eventStore,
        Expression streamId, 
        Expression expectedVersion, 
        Expression events, 
        Expression cancellationToken)
    {
        ValidateParameters(eventStore, streamId, cancellationToken);
        
        //Looks like eventStore.Append(streamId, expectedVersion, events, cancellationToken)
        var method = typeof(IEventStore)
                         .GetMethods()
                         .SingleOrDefault(m =>
                             m.Name == nameof(IEventStore.Append)
                             && m.GetParameters().Length == 4 
                             && typeof(IEnumerable).IsAssignableFrom(m.GetParameters()[2].ParameterType)) 
                     ?? throw new InvalidOperationException("Unable to locate method IEventStore.Append(streamId, expectedVersion, events, cancellationToken)");
        return Expression.Call(eventStore, method, streamId, expectedVersion, events, cancellationToken);
    }

    // public Expression AppendSingle(Expression eventStore,
    //     Expression streamId, 
    //     Expression expectedVersion,
    //     Expression @event,
    //     Expression metadata,
    //     Expression cancellationToken)
    // {
    //     var uncommittedEvent = new NewUncommittedEventExpBuilder(_options)
    //         .BuildCreateUncommittedEventExpression(@event, metadata);
    //     var array = CreateImmutableArray(uncommittedEvent);
    //     return AppendMultiple(eventStore, st)
    // }
    
    #endregion
}