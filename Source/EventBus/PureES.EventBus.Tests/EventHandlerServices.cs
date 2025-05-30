using System.Collections;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PureES.EventBus.Tests;

public class EventHandlerServices : IServiceProvider
{
    private readonly ServiceProvider _services;

    public EventHandlerServices(
        Dictionary<Type, Action<EventEnvelope>[]> handlers,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddPureES();

        services.RemoveAll(typeof(IEventHandlersProvider));
        services.AddSingleton<IEventHandlersProvider>(new EventHandlersProvider(handlers));

        configureServices?.Invoke(services);

        _services = services.BuildServiceProvider();
    }

    public object? GetService(Type serviceType)
    {
        return _services.GetService(serviceType);
    }

    private class EventHandlersProvider : IEventHandlersProvider
    {
        private readonly Dictionary<Type, Action<EventEnvelope>[]> _handlers;

        public EventHandlersProvider(Dictionary<Type, Action<EventEnvelope>[]> handlers)
        {
            _handlers = handlers;
        }

        public IEventHandlerCollection GetHandlers(Type eventType)
        {
            var handlers = _handlers.GetValueOrDefault(eventType, []);
            return new EventHandlerCollection(handlers);
        }
    }

    private class EventHandlerCollection : IEventHandlerCollection
    {
        private readonly List<EventHandler> _handlers;

        public EventHandlerCollection(IEnumerable<Action<EventEnvelope>> handlers) =>
            _handlers = handlers.Select(h => new EventHandler(h)).ToList();

        public IEnumerator<IEventHandler> GetEnumerator() => _handlers.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_handlers).GetEnumerator();

        public int Count => _handlers.Count;

        public IEventHandler this[int index] => _handlers[index];

        public Type EventType => throw new NotImplementedException();
    }

    private class EventHandler : IEventHandler
    {
        private readonly Action<EventEnvelope> _handler;

        public EventHandler(Action<EventEnvelope> handler) => _handler = handler;

        public Task Handle(EventEnvelope @event)
        {
            _handler(@event);
            return Task.CompletedTask;
        }

        public MethodInfo Method => throw new NotImplementedException();
        public int Priority => throw new NotImplementedException();
    }
}