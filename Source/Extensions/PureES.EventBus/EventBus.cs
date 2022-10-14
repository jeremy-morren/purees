using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core;

namespace PureES.EventBus;

internal class EventBus : IEventBus
{
    private readonly IServiceProvider _services;
    private readonly EventHandlerCollection _eventHandlers;

    public EventBus(IServiceProvider services,
        EventHandlerCollection eventHandlers)
    {
        _services = services;
        _eventHandlers = eventHandlers;
    }

    public Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>[] GetRegisteredEventHandlers<TEvent, TMetadata>()
        where TEvent : notnull
        where TMetadata : notnull =>
        _eventHandlers.Get<TEvent, TMetadata>() ?? Array.Empty<Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>>();

    private static readonly ConcurrentDictionary<Type, Func<EventBus, EventEnvelope, CancellationToken, Task>> PublishHandlers = new();

    public Task Publish(EventEnvelope envelope, CancellationToken ct)
    {
        var handler = PublishHandlers.GetOrAdd(envelope.Event.GetType(), 
            t => CompileDelegate(t, envelope.Metadata?.GetType() ?? typeof(object)));
        return handler(this, envelope, ct);
    }

    private static async Task PublishGeneric<TEvent, TMetadata>(EventBus bus, EventEnvelope envelope, CancellationToken ct) 
        where TEvent : notnull
        where TMetadata : notnull
    {
        var handler = bus._services.GetRequiredService<IEventHandler<TEvent, TMetadata>>();
        await handler.Handle(new EventEnvelope<TEvent, TMetadata>(envelope), ct);
    }
    
    private Func<EventBus, EventEnvelope, CancellationToken, Task> CompileDelegate(Type eventType, Type metadataType)
    {
        var busParam = Expression.Parameter(typeof(EventBus), "eventBus");
        var envelopeParam = Expression.Parameter(typeof(EventEnvelope), "envelope");
        var tokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var method = GetType().GetMethod(nameof(PublishGeneric), 
                         BindingFlags.NonPublic | BindingFlags.Static) 
                     ?? throw new InvalidOperationException($"Unable to get {nameof(PublishGeneric)} method");
        method = method.MakeGenericMethod(eventType, metadataType);
        //Looks like (bus, envelope, ct) => Publish<T,T>(bus,envelope,ct)
        var call = Expression.Call(method, busParam, envelopeParam, tokenParam);
        return Expression.Lambda<Func<EventBus, EventEnvelope, CancellationToken, Task>>(call, 
            busParam, envelopeParam, tokenParam).Compile();
    }
}
