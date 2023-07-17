using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PureES.Core;
using Xunit;

namespace PureES.EventBus.Tests;

public class EventBusTests
{
    [Fact]
    public async Task Handle()
    {
        var executed1 = false;
        var executed2 = false;

        var eventHandlers = new EventHandlersCollection(new Dictionary<Type, Action<EventEnvelope>[]>()
        {
            {
                typeof(object), new Action<EventEnvelope>[]
                {
                    _ => executed1 = true,
                    _ => executed2 = true
                }
            }
        });
        
        var services = new ServiceCollection().BuildServiceProvider();

        var bus = new EventBus(new EventBusOptions(), services, eventHandlers);

        await bus.SendAsync(NewEnvelope());
        
        bus.Complete();
        await bus.Completion;
        
        Assert.True(executed1);
        Assert.True(executed2);
    }

    [Fact]
    public async Task Handle_With_No_Event_Handlers()
    {
        var eventHandlers = new EventHandlersCollection(new Dictionary<Type, Action<EventEnvelope>[]>());
        
        var services = new ServiceCollection().BuildServiceProvider();

        var bus = new EventBus(new EventBusOptions(), services, eventHandlers);

        await bus.SendAsync(NewEnvelope());
        
        bus.Complete();
        await bus.Completion;
    }

    [Fact]
    public async Task EventBusEvents()
    {
        var eventHandlers = new EventHandlersCollection(new Dictionary<Type, Action<EventEnvelope>[]>());

        var events = new Mock<IEventBusEvents>();

        var services = new ServiceCollection()
            .AddSingleton(events.Object)
            .BuildServiceProvider();
        
        var bus = new EventBus(new EventBusOptions(), services, eventHandlers);

        var eventEnvelope = NewEnvelope();
        await bus.SendAsync(eventEnvelope);
        bus.Complete();
        await bus.Completion;

        events.Verify(e => e.OnEventHandled(eventEnvelope), Times.Once);
    }

    private static EventEnvelope NewEnvelope() => new(
        Guid.NewGuid(),
        Guid.NewGuid().ToString(),
        0,
        DateTime.UtcNow,
        new Lazy<object>(() => Object),
        new Lazy<object?>(() => Object));

    private static readonly object Object = new();
}