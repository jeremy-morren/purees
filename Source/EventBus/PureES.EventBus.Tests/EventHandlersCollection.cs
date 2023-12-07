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
            var svc = typeof(IEventHandler<>).MakeGenericType(pair.Key);
            var impl = typeof(EventHandler<>).MakeGenericType(pair.Key);
            foreach (var handler in pair.Value)
                services.AddSingleton(svc, Activator.CreateInstance(impl, new object?[] { handler })!);
        }

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

        public MethodInfo Method => throw new NotImplementedException();
    }
}