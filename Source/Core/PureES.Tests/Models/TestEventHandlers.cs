using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace PureES.Tests.Models;

[EventHandlers]
public class TestEventHandlers
{
    private readonly IServiceProvider _services;

    public TestEventHandlers(IServiceProvider services) => _services = services;

    public static Task OnCreated([Event] Events.Created e, CancellationToken ct)
    {
        e.ShouldNotBeNull();
        return Task.CompletedTask;
    }

    [EventHandlerPriority(10)]
    public void OnUpdated(EventEnvelope<Events.Updated> envelope)
    {
        envelope.ShouldNotBeNull();
        _services.ShouldNotBeNull();
    }

    public void OnCreated2(EventEnvelope<Events.Created, object> envelope, [FromServices] ILoggerFactory lf)
    {
        envelope.ShouldNotBeNull();
        _services.ShouldNotBeNull();
        lf.ShouldNotBeNull();
    }

    public static void CatchAll(EventEnvelope envelope)
    {
        envelope.ShouldNotBeNull();
    }
}