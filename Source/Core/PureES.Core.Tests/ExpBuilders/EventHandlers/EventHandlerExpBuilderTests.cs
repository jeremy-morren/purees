using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders.EventHandlers;
using PureES.Core.Tests.ExpBuilders.WhenHandlers;
using Xunit;

// ReSharper disable SuggestBaseTypeForParameter
// ReSharper disable UnusedMember.Local
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
// ReSharper disable UnusedParameter.Local

namespace PureES.Core.Tests.ExpBuilders.EventHandlers;

public class EventHandlerExpBuilderTests
{
    [Theory]
    [InlineData(nameof(EventHandlers.EventHandler))]
    [InlineData(nameof(EventHandlers.EventHandlerValueTask))]
    [InlineData(nameof(EventHandlers.EventHandlerTask))]
    public void InvokeEventHandlerShouldSucceed(string methodName)
    {
        var builder = new EventHandlerExpBuilder(CustomEnvelopeOptions);
        var method = typeof(EventHandlers).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        var func = builder.BuildEventHandlerFactory(method!).Compile();

        var services = new ServiceCollection()
            .AddSingleton<Service>()
            .AddSingleton<EventHandlers>()
            .BuildServiceProvider();
        var envelope = new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            new Lazy<object>(),
            new Lazy<object?>());

        func(envelope, services, default).Wait();
    }
    
    [Theory]
    [InlineData(nameof(EventHandlersCustomEnvelope.EventHandler))]
    [InlineData(nameof(EventHandlersCustomEnvelope.EventHandlerValueTask))]
    [InlineData(nameof(EventHandlersCustomEnvelope.EventHandlerTask))]
    public void InvokeEventHandlerCustomEnvelopeShouldSucceed(string methodName)
    {
        var builder = new EventHandlerExpBuilder(CustomEnvelopeOptions);
        var method = typeof(EventHandlersCustomEnvelope).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        var func = builder.BuildEventHandlerFactory(method!).Compile();

        var services = new ServiceCollection()
            .AddSingleton<Service>()
            .AddSingleton<EventHandlersCustomEnvelope>()
            .BuildServiceProvider();
        var envelope = new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            new Lazy<object>(),
            new Lazy<object?>());

        func(envelope, services, default).Wait();
    }
    
    [Theory]
    [InlineData(nameof(EventHandlersStatic.EventHandler))]
    [InlineData(nameof(EventHandlersStatic.EventHandlerValueTask))]
    [InlineData(nameof(EventHandlersStatic.EventHandlerTask))]
    public void InvokeStaticEventHandlerShouldSucceed(string methodName)
    {
        var builder = new EventHandlerExpBuilder(CustomEnvelopeOptions);
        var method = typeof(EventHandlersStatic).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var func = builder.BuildEventHandlerFactory(method!).Compile();

        var services = new ServiceCollection()
            .AddSingleton<Service>()
            .BuildServiceProvider();
        var envelope = new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            new Lazy<object>(),
            new Lazy<object?>());

        func(envelope, services, default).Wait();
    }
    
    [Theory]
    [InlineData(nameof(EventHandlersStaticCustomEnvelope.EventHandler))]
    [InlineData(nameof(EventHandlersStaticCustomEnvelope.EventHandlerValueTask))]
    [InlineData(nameof(EventHandlersStaticCustomEnvelope.EventHandlerTask))]
    public void InvokeStaticEventHandlerCustomEnvelopeShouldSucceed(string methodName)
    {
        var builder = new EventHandlerExpBuilder(CustomEnvelopeOptions);
        var method = typeof(EventHandlersStaticCustomEnvelope).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var func = builder.BuildEventHandlerFactory(method!).Compile();

        var services = new ServiceCollection()
            .AddSingleton<Service>()
            .BuildServiceProvider();
        var envelope = new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            new Lazy<object>(),
            new Lazy<object?>());

        func(envelope, services, default).Wait();
    }

    public static readonly PureESBuilderOptions CustomEnvelopeOptions = new()
    {
        IsStronglyTypedEventEnvelope = t => t.GetGenericArguments().Length == 1
                                            && typeof(EventEnvelope<>).MakeGenericType(t.GetGenericArguments()) == t
    };
}