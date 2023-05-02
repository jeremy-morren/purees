using Microsoft.Extensions.DependencyInjection;
using PureES.Core.Tests.Models;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders.EventHandlers;

public class EventHandlerServicesBuilderTests
{
    [Fact]
    public void AddEventHandlersCollection()
    {
        var services = new ServiceCollection()
            .AddPureES(new[]
                {
                    typeof(EventHandlerServicesBuilderTests).Assembly
                },
                EventHandlerExpBuilderTests.CustomEnvelopeOptions);
        Assert.All(new[] {typeof(EventHandlers), typeof(EventHandlersCustomEnvelope)},
            t => Assert.Contains(services, s => s.ServiceType == t));
        Assert.All(new[] {typeof(EventHandlersStatic), typeof(EventHandlersStaticCustomEnvelope)},
            t => Assert.DoesNotContain(services, s => s.ServiceType == t));
    }

    [Fact]
    public void MultipleEventHandlersShouldBeRegistered()
    {
        using var services = new ServiceCollection()
            .AddPureES(new[] {typeof(EventHandlerServicesBuilderTests).Assembly},
                EventHandlerExpBuilderTests.CustomEnvelopeOptions)
            .BuildServiceProvider();

        var collection = services.GetRequiredService<IEventHandlersCollection>();

        var handlers = collection.GetEventHandlers(typeof(Events.Created));
        Assert.NotEmpty(handlers);
        Assert.NotEqual(1, handlers.Length);
    }
    
    [Fact]
    public void NoEventHandlersShouldBeEmpty()
    {
        using var services = new ServiceCollection()
            .AddPureES(new[] {typeof(EventHandlerServicesBuilderTests).Assembly},
                EventHandlerExpBuilderTests.CustomEnvelopeOptions)
            .BuildServiceProvider();

        var collection = services.GetRequiredService<IEventHandlersCollection>();

        var handlers = collection.GetEventHandlers(typeof(Events.Updated));
        Assert.Empty(handlers);
    }
}