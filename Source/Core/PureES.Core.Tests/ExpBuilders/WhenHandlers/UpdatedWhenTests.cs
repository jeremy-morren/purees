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

public class UpdatedWhenTests
{
    [Theory]
    [MemberData(nameof(GetAggregateAndUpdatedEventTypes))]
    public void ValidateUpdatedWhenShouldSucceed(Type aggregateType, Type eventType)
    {
        var builder = new UpdatedWhenExpBuilder(TestAggregates.Options);
        var method = TestAggregates.GetMethod(aggregateType, eventType);
        builder.ValidateUpdatedWhen(aggregateType, method);
        Assert.True(builder.IsUpdatedWhen(aggregateType, method));
    }

    [Theory]
    [MemberData(nameof(GetAggregateAndCreatedEventTypes))]
    public void ValidateCreatedWhenShouldFail(Type aggregateType, Type eventType)
    {
        var builder = new UpdatedWhenExpBuilder(TestAggregates.Options);
        var method = TestAggregates.GetMethod(aggregateType, eventType);
        Assert.ThrowsAny<Exception>(() => builder.ValidateUpdatedWhen(aggregateType, method));
        Assert.False(builder.IsUpdatedWhen(aggregateType, method));
    }

    [Theory]
    [MemberData(nameof(GetAggregateAndUpdatedEventTypes))]
    public async Task InvokeSingleMethod(Type aggregateType, Type eventType)
    {
        var method = typeof(UpdatedWhenTests).GetStaticMethod(nameof(InvokeSingleMethodGeneric));
        var task = (Task) method.MakeGenericMethod(aggregateType, eventType)
            .Invoke(null, Array.Empty<object?>())!;
        await task;
    }

    private static async Task InvokeSingleMethodGeneric<TAggregate, TEvent>()
        where TEvent : notnull
        where TAggregate : notnull
    {
        var envelope = new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            Rand.NextULong(),
            DateTime.UtcNow,
            TestAggregates.NewEvent<TEvent>(),
            Metadata.New());

        var svc = new AggregateService
        {
            Event = envelope
        };

        using var sp = new ServiceCollection()
            .AddSingleton(svc)
            .BuildServiceProvider();

        var builder = new UpdatedWhenExpBuilder(TestAggregates.Options);
        var exp = builder.BuildUpdatedWhen(typeof(TAggregate),
            TestAggregates.GetMethod(typeof(TAggregate), typeof(TEvent)),
            Expression.Constant(TestAggregates.NewAggregate<TAggregate>()),
            Expression.Constant(envelope),
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(default(CancellationToken)));
        var func = Expression.Lambda<Func<ValueTask<TAggregate>>>(exp).Compile();

        var agg = await func();
        Assert.NotNull(agg);
        Assert.Equal(new EventEnvelope<TEvent, Metadata>(envelope), TestAggregates.GetEnvelope<TEvent>(agg));
    }

    [Theory]
    [MemberData(nameof(GetAggregateTypes))]
    public async Task InvokeUpdatedWhen(Type aggregateType)
    {
        var method = typeof(UpdatedWhenTests).GetStaticMethod(nameof(InvokeUpdatedWhenGeneric));
        await (Task) method.MakeGenericMethod(aggregateType).Invoke(null, Array.Empty<object?>())!;
    }

    private static async Task InvokeUpdatedWhenGeneric<TAggregate>() where TAggregate : notnull
    {
        var svc = new AggregateService();
        using var sp = new ServiceCollection()
            .AddSingleton(svc)
            .BuildServiceProvider();

        var param = Expression.Parameter(typeof(EventEnvelope));
        var builder = new UpdatedWhenExpBuilder(TestAggregates.Options);
        var exp = builder.BuildUpdateExpression(typeof(TAggregate),
            Expression.Constant(TestAggregates.NewAggregate<TAggregate>()),
            param,
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(default(CancellationToken)));

        var func = Expression.Lambda<Func<EventEnvelope, ValueTask<TAggregate>>>(exp, param).Compile();

        EventEnvelope CreateEnvelope<TEvent>() where TEvent : notnull => new(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            Rand.NextULong(),
            DateTime.UtcNow,
            TestAggregates.NewEvent<TEvent>(),
            Metadata.New());

        var one = CreateEnvelope<Updated1>();
        var two = CreateEnvelope<Updated2>();

        svc.Event = one;
        var agg = await func(one);
        Assert.NotNull(agg);
        TestAggregates.AssertEqual<Updated1>(agg, one);
        Assert.Null(TestAggregates.GetEnvelope<Updated2>(agg));

        svc.Event = two;
        agg = await func(two);
        Assert.NotNull(agg);
        TestAggregates.AssertEqual<Updated2>(agg, two);
        Assert.Null(TestAggregates.GetEnvelope<Updated1>(agg));

        //Test base case (i.e. no event handler found)
        await Assert.ThrowsAsync<ArgumentException>(async () => await func(CreateEnvelope<Created1>()));
        await Assert.ThrowsAsync<ArgumentException>(async () => await func(CreateEnvelope<Created2>()));
    }

    public static IEnumerable<object[]> GetAggregateTypes() => TestAggregates.GetAggregateTypes()
        .Select(t => new object[] {t});

    public static IEnumerable<object[]> GetAggregateAndUpdatedEventTypes() =>
        TestAggregates.GetAggregateAndUpdatedEventTypes();

    public static IEnumerable<object[]> GetAggregateAndCreatedEventTypes() =>
        TestAggregates.GetAggregateAndCreatedEventTypes();
}