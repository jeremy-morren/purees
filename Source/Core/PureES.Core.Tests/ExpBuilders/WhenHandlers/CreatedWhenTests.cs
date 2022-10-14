using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders;
using PureES.Core.ExpBuilders.WhenHandlers;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders.WhenHandlers;

public class CreatedWhenTests
{
    [Theory]
    [MemberData(nameof(GetAggregateAndCreatedEventTypes))]
    public void ValidateCreatedWhenShouldSucceed(Type aggregateType, Type eventType)
    {
        var builder = new CreatedWhenExpBuilder(TestAggregates.Options);
        
        var method = TestAggregates.GetMethod(aggregateType, eventType);
        builder.ValidateCreatedWhen(aggregateType, method);
        Assert.True(builder.IsCreatedWhen(aggregateType, method));
    }

    [Theory]
    [MemberData(nameof(GetAggregateAndUpdatedEventTypes))]
    public void ValidateUpdatedWhenShouldFail(Type aggregateType, Type eventType)
    {
        var method = TestAggregates.GetMethod(aggregateType, eventType);
        var builder = new CreatedWhenExpBuilder(TestAggregates.Options);
        Assert.False(builder.IsCreatedWhen(aggregateType, method));
        Assert.ThrowsAny<Exception>(() => builder.ValidateCreatedWhen(aggregateType, method));
    }

    [Theory]
    [MemberData(nameof(GetAggregateAndCreatedEventTypes))]
    public async Task InvokeSingleMethod(Type aggregateType, Type eventType)
    {
        var method = typeof(CreatedWhenTests).GetStaticMethod(nameof(InvokeSingleMethodGeneric));
        var task = (Task)method.MakeGenericMethod(aggregateType, eventType)
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
            new Metadata(Guid.NewGuid()));

        var svc = new AggregateService()
        {
            Event = envelope
        };
        
        await using var sp = new ServiceCollection()
            .AddSingleton(svc)
            .BuildServiceProvider();

        var builder = new CreatedWhenExpBuilder(TestAggregates.Options);
        var exp = builder.BuildCreatedWhen(typeof(TAggregate), 
            TestAggregates.GetMethod(typeof(TAggregate), typeof(TEvent)),
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
    public async Task InvokeCreatedWhen(Type aggregateType)
    {
        var method = typeof(CreatedWhenTests).GetStaticMethod(nameof(InvokeCreatedWhenGeneric));
        await (Task) method.MakeGenericMethod(aggregateType).Invoke(null, Array.Empty<object?>())!;
    }
    
    private static async Task InvokeCreatedWhenGeneric<TAggregate>() where TAggregate : notnull
    {
        var svc = new AggregateService();
        await using var sp = new ServiceCollection()
            .AddSingleton(svc)
            .BuildServiceProvider();
        
        var param = Expression.Parameter(typeof(EventEnvelope));
        var builder = new CreatedWhenExpBuilder(TestAggregates.Options);
        var exp = builder.BuildCreateExpression(typeof(TAggregate), 
            param,
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(default(CancellationToken)));
        
        var func = Expression.Lambda<Func<EventEnvelope, ValueTask<TAggregate>>>(exp, param).Compile();
    
        EventEnvelope CreateEnvelope<TEvent>() where TEvent : notnull => new (Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            Rand.NextULong(),
            DateTime.UtcNow,
            TestAggregates.NewEvent<TEvent>(),
            new Metadata(Guid.NewGuid()));
    
        var one = CreateEnvelope<Created1>();
        var two = CreateEnvelope<Created2>();

        var agg = await func(one);
        Assert.NotNull(agg);
        TestAggregates.AssertEqual<Created1>(agg, one);
        Assert.Null(TestAggregates.GetEnvelope<Created2>(agg));

        svc.Event = two;
        agg = await func(two);
        Assert.NotNull(agg);
        TestAggregates.AssertEqual<Created2>(agg, two);
        Assert.Null(TestAggregates.GetEnvelope<Created1>(agg));
    
        //Test base case (i.e. no event handler found)
        await Assert.ThrowsAsync<ArgumentException>(async () => await func(CreateEnvelope<Updated1>()));
        await Assert.ThrowsAsync<ArgumentException>(async () => await func(CreateEnvelope<Updated2>()));
    }

     public static IEnumerable<object[]> GetAggregateTypes() => TestAggregates.GetAggregateTypes()
         .Select(t => new object[] {t});
     
     public static IEnumerable<object[]> GetAggregateAndCreatedEventTypes() =>
         TestAggregates.GetAggregateAndCreatedEventTypes();

     public static IEnumerable<object[]> GetAggregateAndUpdatedEventTypes() =>
         TestAggregates.GetAggregateAndUpdatedEventTypes();
}