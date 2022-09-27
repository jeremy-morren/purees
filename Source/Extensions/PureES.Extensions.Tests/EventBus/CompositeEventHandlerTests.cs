using Microsoft.Extensions.DependencyInjection;
using Moq;
using PureES.Core;
using PureES.EventBus;

namespace PureES.Extensions.Tests.EventBus;

public class CompositeEventHandlerTests
{
    [Fact]
    public async Task Multiple_Event_Handlers_Should_Be_Called()
    {
        var handler1 = new Mock<IEventHandler<object, object>>();
        var handler2 = new Mock<IEventHandler<object, object>>();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddEventHandler(_ => handler1.Object)
            .AddEventHandler(_ => handler2.Object)
            .BuildServiceProvider();

        var @event = new EventEnvelope<object, object>(NewEnvelope());
        var ct = new CancellationTokenSource().Token;
        await services.GetRequiredService<IEventHandler<object, object>>().Handle(@event, ct);

        handler1.Verify(h => h.Handle(@event, ct), Times.Once);
        handler2.Verify(h => h.Handle(@event, ct), Times.Once);
    }
    
    public static EventEnvelope NewEnvelope() => new(
        Guid.NewGuid(), 
        Guid.NewGuid().ToString(),
        0,
        DateTime.UtcNow,
        new object(),
        new object());
}