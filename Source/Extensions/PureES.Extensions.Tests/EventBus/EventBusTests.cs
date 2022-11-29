using Microsoft.Extensions.DependencyInjection;
using Moq;
using PureES.Core;
using PureES.EventBus;

namespace PureES.Extensions.Tests.EventBus;

public class EventBusTests
{
    [Fact]
    public async Task Handle()
    {
        var handler1 = new Mock<IEventHandler<object, object>>();
        var handler2 = new Mock<IEventHandler<object, object>>();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddEventBus()
            .AddEventHandler(_ => handler1.Object)
            .AddEventHandler(_ => handler2.Object)
            .BuildServiceProvider();

        var envelope = NewEnvelope();
        var ct = new CancellationTokenSource().Token;
        await services.GetRequiredService<IEventBus>().Publish(envelope, ct);

        handler1.Verify(h => h.Handle(
            It.Is<EventEnvelope<object, object>>(e => e == envelope), ct), Times.Once);
        handler2.Verify(h => h.Handle(
            It.Is<EventEnvelope<object, object>>(e => e == envelope), ct), Times.Once);
    }

    [Fact]
    public async Task Handle_With_No_Event_Handlers()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddEventBus()
            .BuildServiceProvider();

        var envelope = NewEnvelope();
        var ct = new CancellationTokenSource().Token;
        await services.GetRequiredService<IEventBus>().Publish(envelope, ct);
    }

    public static EventEnvelope NewEnvelope() => new(
        Guid.NewGuid(),
        Guid.NewGuid().ToString(),
        0,
        0,
        DateTime.UtcNow,
        new Lazy<object>(() => Object),
        new Lazy<object?>(() => Object));

    private static readonly object Object = new();
}