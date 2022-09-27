using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using PureES.Core.ExpBuilders.WhenHandlers;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Core.Tests.ExpBuilders.WhenHandlers;

public class FactoryExpBuilderTests
{
    private readonly ITestOutputHelper _output;

    public FactoryExpBuilderTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void BuildFactory()
    {
        EventEnvelope New(object @event) => new (Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            @event,
            new Metadata(Guid.NewGuid()));

        var created = New(new Created(Guid.NewGuid()));
        var updated = New(new Updated(Guid.NewGuid()));

        var @events = ImmutableArray.Create(created, updated).AsAsyncEnumerable();
        var ct = new CancellationTokenSource().Token;
        var builder = new FactoryExpBuilder(new CommandHandlerOptions());
        var exp = builder.BuildExpression(typeof(Aggregate), 
            Expression.Constant(@events), 
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<LoadedAggregate<Aggregate>>>>(exp).Compile();
        
        Assert.NotNull(func().GetAwaiter().GetResult());
        var agg = func().GetAwaiter().GetResult();
        Assert.Equal((ulong)1, agg.Revision);
        Assert.True(created.Equals(agg.Aggregate.Created));
        Assert.True(updated.Equals(agg.Aggregate.Updated));
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