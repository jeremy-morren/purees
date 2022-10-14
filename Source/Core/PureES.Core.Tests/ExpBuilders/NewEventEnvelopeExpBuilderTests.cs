using System;
using System.Linq.Expressions;
using PureES.Core.ExpBuilders;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders;

public class NewEventEnvelopeExpBuilderTests
{
    [Fact]
    public void BuildEventEnvelope()
    {
        var current = new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            Rand.NextULong(),
            DateTime.UtcNow,
            new Event(Guid.NewGuid()),
            new Metadata(Guid.NewGuid()));
        var builder = new NewEventEnvelopeExpBuilder(new CommandHandlerBuilderOptions());
        var exp = builder.New(typeof(EventEnvelope<Event, Metadata>),
            Expression.Constant(current));
        var func = Expression.Lambda<Func<EventEnvelope<Event, Metadata>>>(exp).Compile();
        Assert.True(func().Equals(current));
        Assert.True(current.Equals(func()));
    }

    private record Event(Guid Id);

    private record Metadata(Guid Id);
}