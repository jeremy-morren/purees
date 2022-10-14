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

    public Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>[]? Get<TEvent, TMetadata>()
        where TEvent : notnull
        where TMetadata : notnull
    {
        if (!_eventHandlers.TryGetValue(typeof(TEvent), out var factories))
            return null;
        var r = new Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>[factories.Length];
        for (var i = 0; i < factories.Length; i++)
            r[i] = (Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>) factories[i];
        return r;
    }
}