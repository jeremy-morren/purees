namespace PureES.Core.Generators.Models;

internal record EventHandlerCollection
{
    /// <summary>
    /// The type of event that is being handled
    /// </summary>
    public IType? EventType { get; }
    
    /// <summary>
    /// The event handlers
    /// </summary>
    public IReadOnlyList<EventHandler> Handlers { get; }
    
    /// <summary>
    /// The services required by all handlers
    /// </summary>
    public IReadOnlyList<IType> Services { get; }
    
    /// <summary>
    /// The event handler parent types, where handler is not static
    /// </summary>
    public IReadOnlyList<IType> Parents { get; }

    public EventHandlerCollection(IType? eventType, IEnumerable<EventHandler> handlers)
    {
        EventType = eventType;
        Handlers = handlers.ToList();
        if ((eventType == null && Handlers.Any(h => h.Event != null))
            || (eventType != null && Handlers.Any(h => h.Event == null || !h.Event.Equals(eventType))))
            throw new NotImplementedException();
        Services = Handlers
            .SelectMany(h => h.Services)
            .Distinct()
            .ToList();
        
        //Injected parents where method is static
        Parents = Handlers
            .Where(h => !h.Method.IsStatic)
            .Select(h => h.Parent)
            .Distinct()
            .ToList();
    }

    public static IEnumerable<EventHandlerCollection> Create(IEnumerable<EventHandler> handlers) => handlers
        .GroupBy(h => h.Event)
        .Select(g => new EventHandlerCollection(g.Key, g));
}