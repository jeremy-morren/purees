using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using PureES.Core.ExpBuilders.WhenHandlers;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Core.Tests.ExpBuilders.WhenHandlers;

public class WhenExpBuilderTests
{
    private readonly ITestOutputHelper _output;

    public WhenExpBuilderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BuildWhen()
    {
        EventEnvelope New(object @event) => new (Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            @event,
            new Metadata(Guid.NewGuid()));

        var created = New(new Created(Guid.NewGuid()));
        var updated = New(new Updated(Guid.NewGuid()));

        var @events = ImmutableArray.Create(created, updated);
        var builder = new LoadExpBuilder(new CommandHandlerOptions());
        var exp = builder.BuildExpression(typeof(Aggregate), Expression.Constant(@events));
        var func = Expression.Lambda<Func<Aggregate>>(exp).Compile();
        
        Assert.NotNull(func());
        var agg = func();
        Assert.True(created.Equals(agg.Created));
        Assert.True(updated.Equals(agg.Updated));
    }

    private record Aggregate(EventEnvelope<Created, Metadata>? Created, EventEnvelope<Updated, Metadata>? Updated)
    {
        public static Aggregate When(EventEnvelope<Created, Metadata> envelope) =>
            new(envelope, null);

        public static Aggregate When(Aggregate current, EventEnvelope<Updated, Metadata> envelope) =>
            current with {Updated = envelope};
    }

    private record Created(Guid Id);

    private record Updated(Guid Id);

    private record Metadata(Guid Id);
}