using PureES.Core;

namespace PureES.EventBus.Tests;

public class EventHandlersCollection : IEventHandlersCollection
{
    private readonly Dictionary<Type, Action<EventEnvelope>[]> _handlers;

    public EventHandlersCollection(Dictionary<Type, Action<EventEnvelope>[]> handlers) => _handlers = handlers;

    public Func<EventEnvelope, IServiceProvider, CancellationToken, Task>[] GetEventHandlers(Type eventType)
    {
        if (!_handlers.TryGetValue(eventType, out var delegates))
            return Array.Empty<Func<EventEnvelope, IServiceProvider, CancellationToken, Task>>();

        var arr = new Func<EventEnvelope, IServiceProvider, CancellationToken, Task>[delegates.Length];
        for (var i = 0; i < delegates.Length; i++)
        {
            var action = delegates[i];
            arr[i] = (e, _, _) =>
            {
                action(e);
                return Task.CompletedTask;
            };
        }

        return arr;
    }
}