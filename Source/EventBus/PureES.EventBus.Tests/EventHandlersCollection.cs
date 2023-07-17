using PureES.Core;

namespace PureES.EventBus.Tests;

public class EventHandlersCollection : IEventHandlersCollection
{
    private readonly Dictionary<Type, Action<EventEnvelope>[]> _handlers;

    public EventHandlersCollection(Dictionary<Type, Action<EventEnvelope>[]> handlers) => _handlers = handlers;

    public EventHandlerDelegate[] GetEventHandlers(Type eventType)
    {
        if (!_handlers.TryGetValue(eventType, out var delegates))
            return Array.Empty<EventHandlerDelegate>();

        var arr = new EventHandlerDelegate[delegates.Length];
        for (var i = 0; i < delegates.Length; i++)
        {
            var action = delegates[i];
            arr[i] = new EventHandlerDelegate(string.Empty, (e, _, _) =>
            {
                action(e);
                return Task.CompletedTask;
            });
        }

        return arr;
    }
}