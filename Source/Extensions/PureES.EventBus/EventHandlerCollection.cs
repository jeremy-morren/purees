using System.Runtime.CompilerServices;

namespace PureES.EventBus;

internal class EventHandlerCollection
{
    private readonly Dictionary<Type, Delegate[]> _eventHandlers = new();

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void AddEventHandler<TEvent, TMetadata>(Func<IServiceProvider, IEventHandler<TEvent, TMetadata>> factory)
        where TEvent : notnull
        where TMetadata : notnull
    {
        if (_eventHandlers.TryGetValue(typeof(TEvent), out var arr))
        {
            Array.Resize(ref arr, arr.Length + 1);
            arr[^1] = factory;
            _eventHandlers[typeof(TEvent)] = arr;
        }
        else
        {
            _eventHandlers.Add(typeof(TEvent), new Delegate[] {factory});
        }
    }

    public IEventHandler<TEvent, TMetadata>[]? Resolve<TEvent, TMetadata>(IServiceProvider services)
        where TEvent : notnull
        where TMetadata : notnull
    {
        if (!_eventHandlers.TryGetValue(typeof(TEvent), out var factories))
            return null;
        var handlers = new IEventHandler<TEvent, TMetadata>[factories.Length];
        for (var i = 0; i < handlers.Length; i++)
        {
            var factory = (Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>) factories[i];
            handlers[i] = factory(services);
        }
        return handlers;
    }
}