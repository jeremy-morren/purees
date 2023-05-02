using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PureES.Core.Tests.ExpBuilders.WhenHandlers;
using PureES.Core.Tests.Models;
using Xunit;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.Core.Tests.ExpBuilders.EventHandlers;

public class Service {}

public static class EventHandlersStatic
{
    [EventHandler]
    public static void EventHandler(EventEnvelope<Events.Created, object> @event,
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(service);
    }

    [EventHandler]
    public static Task EventHandlerTask(EventEnvelope<Events.Created, object> @event,
        [FromServices] Service service,
        CancellationToken _)
    {
        Assert.NotNull(@event);
        Assert.NotNull(service);
        return Task.CompletedTask;
    }

    [EventHandler]
    public static ValueTask EventHandlerValueTask(EventEnvelope<Events.Created, object> @event,
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(service);
        return ValueTask.CompletedTask;
    }
}
    
public static class EventHandlersStaticCustomEnvelope
{
    [EventHandler]
    public static void EventHandler(EventEnvelope<Events.Created> @event, 
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(service);
    }
        
    [EventHandler]
    public static Task EventHandlerTask(EventEnvelope<Events.Created> @event, 
        [FromServices] Service service,
        CancellationToken _)
    {
        Assert.NotNull(@event);
        Assert.NotNull(service);
        return Task.CompletedTask;
    }

    [EventHandler]
    public static ValueTask EventHandlerValueTask(EventEnvelope<Events.Created> @event, 
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(service);
        return ValueTask.CompletedTask;
    }
}

public class EventHandlers
{
    private readonly IServiceProvider _services;

    public EventHandlers(IServiceProvider services) => _services = services;

    [EventHandler]
    public void EventHandler(EventEnvelope<Events.Created, object> @event,
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(_services);
        Assert.NotNull(service);
    }
    
    [EventHandler]
    public Task EventHandlerTask(EventEnvelope<Events.Created, object> @event,
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(_services);
        Assert.NotNull(service);
        return Task.CompletedTask;
    }
    
    [EventHandler]
    public ValueTask EventHandlerValueTask(EventEnvelope<Events.Created, object> @event,
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(_services);
        Assert.NotNull(service);
        return ValueTask.CompletedTask;
    }
}

public class EventHandlersCustomEnvelope
{
    private readonly IServiceProvider _services;

    public EventHandlersCustomEnvelope(IServiceProvider services) => _services = services;

    [EventHandler]
    public void EventHandler(EventEnvelope<Events.Created> @event,
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(_services);
        Assert.NotNull(service);
    }
    
    [EventHandler]
    public Task EventHandlerTask(EventEnvelope<Events.Created> @event,
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(_services);
        Assert.NotNull(service);
        return Task.CompletedTask;
    }
    
    [EventHandler]
    public ValueTask EventHandlerValueTask(EventEnvelope<Events.Created> @event,
        [FromServices] Service service)
    {
        Assert.NotNull(@event);
        Assert.NotNull(_services);
        Assert.NotNull(service);
        return ValueTask.CompletedTask;
    }
}