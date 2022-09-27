using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using PureES.Core.ExpBuilders.WhenHandlers;
using Xunit;

// ReSharper disable NotAccessedPositionalProperty.Local

namespace PureES.Core.Tests.ExpBuilders.WhenHandlers;

public class CreatedWhenTests
{
    [Fact]
    public void ValidateCreatedWhen()
    {
        var builder = new CreatedWhenExpBuilder(new CommandHandlerOptions());
        Assert.True(builder.IsCreatedWhen(typeof(Aggregate), Aggregate.CreatedWhenOne));
        Assert.True(builder.IsCreatedWhen(typeof(Aggregate), Aggregate.CreatedWhenTwo));
        
        builder.ValidateCreatedWhen(typeof(Aggregate), Aggregate.CreatedWhenOne);
        builder.ValidateCreatedWhen(typeof(Aggregate), Aggregate.CreatedWhenTwo);
    }

    [Fact]
    public void Validate_UpdatedWhen_ShouldFail()
    {
        var builder = new CreatedWhenExpBuilder(new CommandHandlerOptions());
        Assert.False(builder.IsCreatedWhen(typeof(Aggregate), Aggregate.WhenThree));
        Assert.ThrowsAny<Exception>(() => builder.ValidateCreatedWhen(typeof(Aggregate), Aggregate.WhenThree));
    }

    [Fact]
    public void InvokeCreatedWhenMethod()
    {
        var envelope = new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            new Created1(Guid.NewGuid()),
            new Metadata(Guid.NewGuid()));
        var builder = new CreatedWhenExpBuilder(new CommandHandlerOptions());
        var exp = builder.BuildCreatedWhen(typeof(Aggregate), 
            Aggregate.CreatedWhenOne,
            Expression.Constant(envelope));
        var func = Expression.Lambda<Func<Aggregate>>(exp).Compile();

        var agg = func();
        Assert.NotNull(agg);
        Assert.NotNull(agg.One);
        Assert.True(envelope.Equals(agg.One));
        Assert.Null(agg.Two);
    }

    [Fact]
    public void InvokeCreatedWhen()
    {
        var param = Expression.Parameter(typeof(EventEnvelope));
        var builder = new CreatedWhenExpBuilder(new CommandHandlerOptions());
        var exp = builder.BuildCreateExpression(typeof(Aggregate), param);
        var func = Expression.Lambda<Func<EventEnvelope, Aggregate>>(exp, param).Compile();

        var one = new Created1(Guid.NewGuid());
        var two = new Created2(Guid.NewGuid());
        var three = new Created3(Guid.NewGuid());

        EventEnvelope CreateEnvelope(object @event) => new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            @event,
            new Metadata(Guid.NewGuid()));

        var agg = func(CreateEnvelope(one));
        Assert.NotNull(agg.One);
        Assert.Equal(one, agg.One!.Event);
        agg = func(CreateEnvelope(two));
        Assert.Null(agg.One);
        Assert.NotNull(agg.Two);
        Assert.Equal(two, agg.Two!.Event);

        Assert.Throws<ArgumentException>(() => func(CreateEnvelope(three)));
    }

    private record Aggregate(EventEnvelope<Created1, Metadata>? One, EventEnvelope<Created2, Metadata>? Two)
    {
        public static Aggregate When(EventEnvelope<Created1, Metadata> envelope) =>
            new(envelope, null);
        public static Aggregate When(EventEnvelope<Created2, Metadata> envelope) =>
            new(null, envelope);

        public static Aggregate When(Aggregate _, EventEnvelope<Created3, Metadata> __) =>
            throw new Exception("Invalid create method");

        private static MethodInfo GetMethod(Type eventType) => typeof(Aggregate)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(When) 
                         && m.GetParameters().Length == 1
                         && m.GetParameters()[0].ParameterType.GetGenericArguments()[0] == eventType);

        public static readonly MethodInfo CreatedWhenOne = GetMethod(typeof(Created1));

        public static readonly MethodInfo CreatedWhenTwo = GetMethod(typeof(Created2));

        public static readonly MethodInfo WhenThree = typeof(Aggregate)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(When) && m.GetParameters().Length == 2);
    }

    private record Created1(Guid Id);

    private record Created2(Guid Id);
    
    private record Created3(Guid Id);

    private record Metadata(Guid Id);
}