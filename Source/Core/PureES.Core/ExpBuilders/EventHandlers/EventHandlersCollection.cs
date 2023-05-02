namespace PureES.Core.ExpBuilders.EventHandlers;

internal class EventHandlersCollection : IEventHandlersCollection
{
    private readonly PureESServices _services;

    public EventHandlersCollection(PureESServices services) => _services = services;

    public Func<EventEnvelope, IServiceProvider, CancellationToken, Task>[] GetEventHandlers(Type eventType)
    {
        var handlers = _services
            .GetRequiredService<Dictionary<Type, Func<EventEnvelope, IServiceProvider, CancellationToken, Task>[]>>();
        
        if (handlers.TryGetValue(eventType, out var delegates))
            return delegates;
        return Array.Empty<Func<EventEnvelope, IServiceProvider, CancellationToken, Task>>();
    }
}