using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Shouldly;

// ReSharper disable PartialTypeWithSinglePart

namespace PureES.Core.Tests.Models;

[PublicAPI]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public partial class TestAggregates
{
    public record Metadata;

    public class EventEnvelope<TEvent> : EventEnvelope<TEvent, Metadata> where TEvent : notnull
    {
        public EventEnvelope(EventEnvelope source) : base(source) {}
    }

    [Aggregate, PublicAPI]
    public partial class Aggregate
    {
        public required EventEnvelope<Events.Created, Metadata> Created { get; init; }
        public Events.Updated? Updated { get; private set; }
        public ulong StreamPosition { get; private set; }

        public static Events.Created CreateOn([Command] Commands.Create cmd) => new(cmd.Id, cmd.Value);

        public Task<Events.Updated[]> UpdateOn([Command] Commands.Update cmd, [FromServices] IServiceProvider service, CancellationToken ct)
        {
            Created.ShouldNotBeNull();
            service.ShouldNotBeNull();
            return Task.FromResult(new[] { new Events.Updated(cmd.Id, cmd.Value) });
        }
        
        public CommandResult<Events.Updated, int[]> UpdateOnResult([Command] Commands.UpdateConstantStream cmd, [FromServices] IServiceProvider service)
        {
            Created.ShouldNotBeNull();
            service.ShouldNotBeNull();
            return new CommandResult<Events.Updated, int[]>(
                new Events.Updated(Created.Event.Id, cmd.Value),
                new [] { cmd.Value });
        }
        
        public static IAsyncEnumerable<Events.Created> CreateOnAsyncEnumerable([Command] int[] cmd, [FromServices] ILoggerFactory lf)
        {
            lf.ShouldNotBeNull();
            return AsyncEnumerable.Empty<Events.Created>();
        }

        public void GlobalWhen(EventEnvelope envelope, CancellationToken ct)
        {
            StreamPosition = envelope.StreamPosition;
        }
        
        public Task GlobalWhenAsync(EventEnvelope envelope, [FromServices] ILoggerFactory lf)
        {
            lf.ShouldNotBeNull();
            StreamPosition = envelope.StreamPosition;
            return Task.CompletedTask;
        }

        public static Aggregate When(EventEnvelope<Events.Created, Metadata> envelope)
        {
            return new Aggregate()
            {
                Created = envelope
            };
        }

        public void When([Event] Events.Updated e, [FromServices] IServiceProvider service)
        {
            e.ShouldNotBeNull();
            service.ShouldNotBeNull();
            Updated = e;
        }
        
        public Task When(EventEnvelope<int> envelope, [FromServices] ILoggerFactory service)
        {
            envelope.ShouldNotBeNull();
            service.ShouldNotBeNull();
            StreamPosition.ShouldNotBe(envelope.StreamPosition);
            return Task.CompletedTask;
        }
    }

    public class EventHandlers
    {
        private readonly IServiceProvider _services;

        public EventHandlers(IServiceProvider services) => _services = services;

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
}