using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using PureES.Core;

namespace PureES.EventBus;

public class EventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;

    public EventBus(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    private static readonly ConcurrentDictionary<Type, Func<EventBus, EventEnvelope, CancellationToken, Task>> PublishHandlers = new();

    public Task Publish(EventEnvelope envelope, CancellationToken ct)
    {
        if (PublishHandlers.TryGetValue(envelope.Event.GetType(), out var handler))
            return handler(this, envelope, ct);
        handler = CompileDelegate(envelope.Event.GetType(), envelope.Metadata?.GetType() ?? typeof(object));
        PublishHandlers.AddOrUpdate(envelope.Event.GetType(), handler, (_, _) => handler);
        return handler(this, envelope, ct);
    }

    private static async Task PublishGeneric<TEvent, TMetadata>(EventBus bus, EventEnvelope envelope, CancellationToken ct) 
        where TEvent : notnull
        where TMetadata : notnull
    {
        using var scope = bus._serviceProvider.CreateScope();
        
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
