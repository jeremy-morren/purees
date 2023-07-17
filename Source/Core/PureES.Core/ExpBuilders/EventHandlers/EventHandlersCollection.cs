namespace PureES.Core.ExpBuilders.EventHandlers;

internal class EventHandlersCollection : IEventHandlersCollection
{
    private readonly PureESServices _services;

    public EventHandlersCollection(PureESServices services) => _services = services;

    public EventHandlerDelegate[] GetEventHandlers(Type eventType)
    {
        var handlers = _services
            .GetRequiredService<Dictionary<Type, EventHandlerDelegate[]>>();

        if (handlers.TryGetValue(eventType, out var delegates))
            return delegates;
        return Array.Empty<EventHandlerDelegate>();
    }
}