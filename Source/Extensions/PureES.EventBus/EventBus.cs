using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core;

namespace PureES.EventBus;

internal class EventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EventHandlerCollection _eventHandlers;

    public EventBus(IServiceProvider serviceProvider,
        EventHandlerCollection eventHandlers)
    {
        _serviceProvider = serviceProvider;
        _eventHandlers = eventHandlers;
    }

    public IEventHandler<TEvent, TMetadata>[] GetRegisteredEventHandlers<TEvent, TMetadata>()
        where TEvent : notnull
        where TMetadata : notnull 
        => _eventHandlers.Resolve<TEvent, TMetadata>(_serviceProvider) ?? 
           Array.Empty<IEventHandler<TEvent, TMetadata>>();

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
        await using var scope = bus._serviceProvider.CreateAsyncScope();
        
        var handler = scope.ServiceProvider.GetService<IEventHandler<TEvent, TMetadata>>();
        if (handler != null)
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
