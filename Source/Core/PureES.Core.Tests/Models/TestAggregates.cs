using System;
using JetBrains.Annotations;
using Shouldly;

// ReSharper disable PartialTypeWithSinglePart

namespace PureES.Core.Tests.Models;

[PublicAPI]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public partial class TestAggregates
{
    public record Metadata;

    public record Result(TestAggregateId Id);

    [Aggregate, PublicAPI]
    public partial class Aggregate
    {
        public required EventEnvelope<Events.Created, Metadata> Created { get; init; }
        public Events.Updated? Updated { get; private set; }
        public ulong StreamPosition { get; private set; }

        public static Events.Created CreateOn([Command] Commands.Create cmd) => new(cmd.Id, cmd.Value);

        public Events.Updated UpdateOn([Command] Commands.Update cmd, [FromServices] IServiceProvider service)
        {
            Created.ShouldNotBeNull();
            service.ShouldNotBeNull();
            return new Events.Updated(cmd.Id, cmd.Value);
        }

        public void When(EventEnvelope envelope)
        {
            StreamPosition = envelope.StreamPosition;
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
    }
}