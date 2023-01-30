using System;
using System.Collections.Generic;
using System.Linq;
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
        await using var sp = new ServiceCollection()
            .AddSingleton(svc)
            .BuildServiceProvider();

        var param = Expression.Parameter(typeof(IAsyncEnumerable<EventEnvelope>));
        var builder = new FactoryExpBuilder(TestAggregates.Options);
        var exp = builder.BuildExpression(typeof(TAggregate),
            param,
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(default(CancellationToken)));

        var func = Expression.Lambda<Func<IAsyncEnumerable<EventEnvelope>,
            ValueTask<TAggregate>>>(exp, param).Compile();

        EventEnvelope CreateEnvelope<TEvent>() where TEvent : notnull => new(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            TestAggregates.NewEvent<TEvent>(),
            Metadata.New());

        var events = new[]
        {
            CreateEnvelope<TCreated>(), 
            CreateEnvelope<Updated1>(), 
            CreateEnvelope<Updated2>()
        };
        
        Assert.All(Enumerable.Range(0, events.Length), i =>
        {
            var agg = func(events.Take(i + 1).AsAsyncEnumerable())
                .AsTask().GetAwaiter().GetResult();
            var @event = events[i];
            TestAggregates.AssertEqual(agg, @event);
            TestAggregates.AssertWhen(agg, @event);
        });
    }
}