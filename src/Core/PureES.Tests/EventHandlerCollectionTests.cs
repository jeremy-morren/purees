using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace PureES.Tests;

public class EventHandlerCollectionTests
{
    [Fact]
    public void ShouldIncludeBaseTypes()
    {
        var handler = new EventHandler<EventBase>(1);
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<EventBase>>(handler);
        services.AddTransient(typeof(EventHandlerCollection<>));

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<EventHandlerCollection<EventDerived>>().ShouldHaveSingleItem().ShouldBe(handler);
    }

    [Fact]
    public void ShouldBeSortedByPriority()
    {
        var handler1 = new EventHandler<EventBase>(1);
        var handler2 = new EventHandler<EventDerived>(2);
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<EventBase>>(handler1);
        services.AddSingleton<IEventHandler<EventDerived>>(handler2);
        services.AddTransient(typeof(EventHandlerCollection<>));

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<EventHandlerCollection<EventDerived>>()
            .Should().BeEquivalentTo(new IEventHandler[] { handler1, handler2 });
    }

    [Fact]
    public void ShouldIncludeCatchAllHandlers()
    {
        var typedHandler = new EventHandler<EventBase>(2);
        var catchAllHandler = new EventHandler(0);
        var catchAllHandler2 = new EventHandler2(1);
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<EventBase>>(typedHandler);
        services.AddSingleton<IEventHandler>(catchAllHandler);
        services.AddSingleton<IEventHandler>(catchAllHandler2);
        services.AddTransient(typeof(EventHandlerCollection<>));

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<EventHandlerCollection<EventDerived>>()
            .Should().BeEquivalentTo(new IEventHandler[] { catchAllHandler, catchAllHandler2, typedHandler });
    }

    [Fact]
    public void ShouldIncludeInterfaces()
    {
        var handlerBase = new EventHandler<EventBase>(5);
        var handlerIBase = new EventHandler<IEventBase>(3);
        var handlerIDerived = new EventHandler<IEventDerived>(1);
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<EventBase>>(handlerBase);
        services.AddSingleton<IEventHandler<IEventBase>>(handlerIBase);
        services.AddSingleton<IEventHandler<IEventDerived>>(handlerIDerived);
        services.AddTransient(typeof(EventHandlerCollection<>));

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<EventHandlerCollection<EventBase>>()
            .Should()
            .BeEquivalentTo(new IEventHandler[] { handlerIBase, handlerBase });
        sp.GetRequiredService<EventHandlerCollection<EventDerived>>()
            .Should()
            .BeEquivalentTo(new IEventHandler[] { handlerIDerived, handlerIBase, handlerBase });

        sp.GetRequiredService<EventHandlerCollection<IEventBase>>()
            .Should()
            .BeEquivalentTo(new IEventHandler[] { handlerIBase });
        sp.GetRequiredService<EventHandlerCollection<IEventDerived>>()
            .Should()
            .BeEquivalentTo(new IEventHandler[] { handlerIDerived, handlerIBase });
    }

    [Fact]
    public void GetHandlersViaProvider()
    {
        var handlerBase = new EventHandler<EventBase>(1);
        var handlerIBase = new EventHandler<IEventBase>(2);
        var handlerIDerived = new EventHandler<IEventDerived>(3);
        var handlerIDerived2 = new EventHandler2<IEventDerived>(4);

        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<EventBase>>(handlerBase);
        services.AddSingleton<IEventHandler<IEventBase>>(handlerIBase);
        services.AddSingleton<IEventHandler<IEventDerived>>(handlerIDerived);
        services.AddSingleton<IEventHandler<IEventDerived>>(handlerIDerived2);

        services.AddPureES();

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IEnumerable<IEventHandler<IEventDerived>>>()
            .Should().BeEquivalentTo(new IEventHandler<IEventDerived>[] { handlerIDerived, handlerIDerived2 });

        var provider = sp.GetRequiredService<IEventHandlersProvider>();

        provider.GetHandlers(typeof(EventBase))
            .Should().BeEquivalentTo(new IEventHandler[] { handlerBase, handlerIBase });
        provider.GetHandlers(typeof(EventDerived))
            .Should().BeEquivalentTo(new IEventHandler[] { handlerBase, handlerIBase, handlerIDerived, handlerIDerived2  });

        provider.GetHandlers(typeof(IEventBase))
            .Should().BeEquivalentTo(new IEventHandler[] { handlerIBase });
        provider.GetHandlers(typeof(IEventDerived))
            .Should().BeEquivalentTo(new IEventHandler[] { handlerIBase, handlerIDerived, handlerIDerived2 });
    }

    private class EventBase : IEventBase;

    private class EventDerived : EventBase, IEventDerived;

    private interface IEventBase;

    private interface IEventDerived : IEventBase;

    private class EventHandler(int priority) : IEventHandler
    {
        public int Priority => priority;
        public MethodInfo Method => throw new NotImplementedException();
        public Task Handle(EventEnvelope @event) => throw new NotImplementedException();

        public override string ToString() => priority.ToString();
    }

    private class EventHandler2(int priority) : EventHandler(priority);

    private class EventHandler<T>(int priority) : IEventHandler<T>
    {
        public int Priority => priority;
        public MethodInfo Method => null!;
        public Task Handle(EventEnvelope @event) => throw new NotImplementedException();

        public override string ToString() => $"{typeof(T).Name} ({Priority})";
    }

    private class EventHandler2<T>(int priority) : EventHandler<T>(priority);
}