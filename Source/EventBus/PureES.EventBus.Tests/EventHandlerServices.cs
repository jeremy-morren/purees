using System.Collections;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PureES.EventBus.Tests;

public class EventHandlerServices : IServiceProvider
{
    private readonly ServiceProvider _services;

    public EventHandlerServices(Dictionary<Type, Action<EventEnvelope>[]> handlers,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        foreach (var pair in handlers)
        {
            var impl = typeof(EventHandler<>).MakeGenericType(pair.Key);
            foreach (var handler in pair.Value)
                services.AddSingleton(typeof(IEventHandler), Activator.CreateInstance(impl, handler)!);
        }

        services.AddTransient(typeof(IEventHandlerCollection<>), typeof(HandlerCollection<>));

        configureServices?.Invoke(services);

        _services = services.BuildServiceProvider();
    }

    public object? GetService(Type serviceType)
    {
        return _services.GetService(serviceType);
    }
    
    private class EventHandler<TEvent> : IEventHandler<TEvent>
    {
        private readonly Action<EventEnvelope> _handler;

        public EventHandler(Action<EventEnvelope> handler) => _handler = handler;

        public Task Handle(EventEnvelope @event)
        {
            _handler(@event);
            return Task.CompletedTask;
        }

        public bool CanHandle(EventEnvelope @event) => @event.Event is TEvent;

        public MethodInfo Method => throw new NotImplementedException();
        public int Priority => 0;
    }

    private class HandlerCollection<TEvent> : IEventHandlerCollection<TEvent>
    {
        private readonly List<IEventHandler> _handlers;

        public HandlerCollection(IEnumerable<IEventHandler> handlers)
        {
            _handlers = handlers.ToList();
        }

        public IEnumerable<IEventHandler> GetHandlers(EventEnvelope @event) => _handlers.Where(h => h.CanHandle(@event));

        public IEnumerator<IEventHandler> GetEnumerator()
        {
            return _handlers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_handlers).GetEnumerator();
        }

        public int Count => _handlers.Count;

        public IEventHandler this[int index] => _handlers[index];
    }
}