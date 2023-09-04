using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PureES.Core;
using Shouldly;
using Xunit;

namespace PureES.EventBus.Tests;

public class EventBusTests
{
    [Fact]
    public async Task Handle()
    {
        var executed1 = false;
        var executed2 = false;

        var services = new EventHandlerServices(new Dictionary<Type, Action<EventEnvelope>[]>()
        {
            {
                typeof(object), new Action<EventEnvelope>[]
                {
                    _ => executed1 = true,
                    _ => executed2 = true
                }
            }
        });

        var bus = new EventBus(new EventBusOptions(), services);

        await bus.SendAsync(NewEnvelope());
        
        bus.Complete();
        await bus.Completion;
        
        executed1.ShouldBeTrue();
        executed2.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_With_No_Event_Handlers()
    {
        var services = new EventHandlerServices(new Dictionary<Type, Action<EventEnvelope>[]>());

        var bus = new EventBus(new EventBusOptions(), services);

        await bus.SendAsync(NewEnvelope());
        
        bus.Complete();
        await bus.Completion;
    }

    [Fact]
    public async Task Events_Should_Propagate_To_EventBusEvents()
    {
        var events = new Mock<IEventBusEvents>();
        
        var services = new EventHandlerServices(new Dictionary<Type, Action<EventEnvelope>[]>(),
            s => s.AddSingleton(events.Object));
        
        var bus = new EventBus(new EventBusOptions(), services);

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