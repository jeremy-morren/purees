using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace PureES;

/// <summary>
/// All event handlers registered for a given event type ordered by priority.
/// </summary>
/// <typeparam name="TEvent">The derived event type</typeparam>
internal class EventHandlerCollection<TEvent> : IEventHandlerCollection
{
    private readonly List<IEventHandler> _handlers;

    public EventHandlerCollection(IServiceProvider services, 
        IEnumerable<IEventHandler>? catchAllHandlers = null)
    {
        _handlers = HandlerFactory(services)
            .SelectMany(l => l ?? [])
            .Concat(catchAllHandlers ?? [])
            .DistinctBy(h => h.GetType())
            .OrderBy(l => l.Priority)
            .ToList();
    }

    public Type EventType => typeof(TEvent);

    #region Factory


    private static readonly List<Type> HandlerTypes = GetTypeHierarchy().Concat(GetInterfaces()).ToList();

    [SuppressMessage("ReSharper", "StaticMemberInGenericType")] 
    private static readonly Func<IServiceProvider, IEnumerable<IEventHandler>?[]> HandlerFactory = CreateHandlerFactory();

    private static Func<IServiceProvider, IEnumerable<IEventHandler>?[]> CreateHandlerFactory()
    {
        var sp = Expression.Parameter(typeof(IServiceProvider), "sp");
        
        //Get event handlers for the type, and all its base types
        var handlers = HandlerTypes
            .Select(t =>
            {
                var enumerable = typeof(IEnumerable<>).MakeGenericType(typeof(IEventHandler<>).MakeGenericType(t));
                Expression body = Expression.Call(sp, ReflectionItems.GetService, Expression.Constant(enumerable));
                return Expression.Convert(body, typeof(IEnumerable<IEventHandler>));
            });
        
        var handlerList = Expression.NewArrayInit(typeof(IEnumerable<IEventHandler>), handlers);

        return Expression.Lambda<Func<IServiceProvider, IEnumerable<IEventHandler>?[]>>(handlerList, sp).Compile();
    }
    
    /// <summary>
    /// Gets all event handlers for the given event type
    /// </summary>
    private static IEnumerable<Type> GetTypeHierarchy()
    {
        var t = typeof(TEvent);
        while (t != null && t != typeof(object))
        {
            yield return t;
            t = t.BaseType;
        }
    }

    private static IEnumerable<Type> GetInterfaces() => typeof(TEvent).GetInterfaces()
        .Where(i => !i.IsGenericType || i.GetGenericTypeDefinition() != typeof(IEquatable<>));
    
    #endregion
    
    #region IReadOnlyList<IEventHandler>

    public IEnumerator<IEventHandler> GetEnumerator() => _handlers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => _handlers.Count;

    public IEventHandler this[int index] => _handlers[index];
    
    
    #endregion
}