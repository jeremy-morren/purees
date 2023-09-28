using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace PureES.Core.Tests.Models;

[PublicAPI]
public class TestEventHandlers
{
    private readonly IServiceProvider _services;

    public TestEventHandlers(IServiceProvider services) => _services = services;

    [EventHandler]
    public static Task OnCreated([Event] Events.Created e, CancellationToken ct)
    {
        e.ShouldNotBeNull();
        return Task.CompletedTask;
    }

    [EventHandler]
    public void OnUpdated(EventEnvelope<Events.Updated> envelope)
    {
        envelope.ShouldNotBeNull();
        _services.ShouldNotBeNull();
    }

    [EventHandler]
    public void OnCreated2(EventEnvelope<Events.Created, object> envelope, [FromServices] ILoggerFactory lf)
    {
        envelope.ShouldNotBeNull();
        _services.ShouldNotBeNull();
        lf.ShouldNotBeNull();
    }
}