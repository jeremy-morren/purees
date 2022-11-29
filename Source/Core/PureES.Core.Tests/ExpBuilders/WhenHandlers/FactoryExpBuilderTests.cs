using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders;
using PureES.Core.ExpBuilders.WhenHandlers;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders.WhenHandlers;

public class FactoryExpBuilderTests
{
    public static IEnumerable<object[]> GetAggregateAndCreatedEventTypes =>
        TestAggregates.GetAggregateAndCreatedEventTypes();

    [Theory]
    [MemberData(nameof(GetAggregateAndCreatedEventTypes))]
    public async Task BuildFactory(Type aggregateType, Type eventType)
    {
        var method = typeof(FactoryExpBuilderTests).GetStaticMethod(nameof(InvokeFactoryGeneric));
        var task = method.MakeGenericMethod(aggregateType, eventType).Invoke(null, Array.Empty<object>());
        await (Task) task!;
    }

    private static async Task InvokeFactoryGeneric<TAggregate, TCreated>()
        where TAggregate : notnull
        where TCreated : notnull
    {
        var svc = new AggregateService();
        using var sp = new ServiceCollection()
            .AddSingleton(svc)
            .BuildServiceProvider();

        var param = Expression.Parameter(typeof(IAsyncEnumerable<EventEnvelope>));
        var builder = new FactoryExpBuilder(TestAggregates.Options);
        var exp = builder.BuildExpression(typeof(TAggregate),
            param,
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(default(CancellationToken)));

        var func = Expression.Lambda<Func<IAsyncEnumerable<EventEnvelope>,
            ValueTask<LoadedAggregate<TAggregate>>>>(exp, param).Compile();

        EventEnvelope CreateEnvelope<TEvent>() where TEvent : notnull => new(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            Rand.NextULong(),
            DateTime.UtcNow,
            TestAggregates.NewEvent<TEvent>(),
            Metadata.New());

        var created = CreateEnvelope<TCreated>();
        var updated1 = CreateEnvelope<Updated1>();
        var updated2 = CreateEnvelope<Updated2>();

        var agg = await func(new[] {created, updated1, updated2}.AsAsyncEnumerable());

        Assert.NotNull(agg);
        Assert.Equal((ulong) 3, agg.Version);

        TestAggregates.AssertEqual<TCreated>(agg.Aggregate, created);
        TestAggregates.AssertEqual<Updated1>(agg.Aggregate, updated1);
        TestAggregates.AssertEqual<Updated2>(agg.Aggregate, updated2);
    }
}