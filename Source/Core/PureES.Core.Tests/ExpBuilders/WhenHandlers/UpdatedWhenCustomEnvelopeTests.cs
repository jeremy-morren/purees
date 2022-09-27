using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using PureES.Core.ExpBuilders.WhenHandlers;
using Xunit;

// ReSharper disable NotAccessedPositionalProperty.Local
// ReSharper disable UnusedParameter.Local

namespace PureES.Core.Tests.ExpBuilders.WhenHandlers;

public class UpdatedWhenCustomEnvelopeTests
{
    private static CommandHandlerOptions Options => new ()
    {
        IsEventEnvelope = type => type.GetGenericArguments().Length == 1 
                                  && typeof(EventEnvelope<>).MakeGenericType(type.GetGenericArguments()[0]) == type,
        GetEventType = envelope => envelope.GetGenericArguments()[0]
    };
    
    [Fact]
    public void ValidateUpdatedWhen()
    {
        var builder = new UpdatedWhenExpBuilder(Options);
        Assert.True(builder.IsUpdatedWhen(typeof(Aggregate), Aggregate.OneMethod));
        Assert.True(builder.IsUpdatedWhen(typeof(Aggregate), Aggregate.TwoMethod));
        builder.ValidateUpdatedWhen(typeof(Aggregate), Aggregate.OneMethod);
        builder.ValidateUpdatedWhen(typeof(Aggregate), Aggregate.TwoMethod);
    }
    
    [Fact]
    public void Validate_CreatedWhen_ShouldFail()
    {
        var builder = new UpdatedWhenExpBuilder(Options);
        Assert.False(builder.IsUpdatedWhen(typeof(Aggregate), Aggregate.ThreeMethod));
        Assert.ThrowsAny<Exception>(() => builder.ValidateUpdatedWhen(typeof(Aggregate), Aggregate.ThreeMethod));
    }
    
    [Fact]
    public void InvokeUpdatedWhenMethod()
    {
        var envelope = new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            new Updated1(Guid.NewGuid()),
            new Metadata(Guid.NewGuid()));
        var current = new Aggregate(null, null);
        var builder = new UpdatedWhenExpBuilder(Options);
        var exp = builder.BuildUpdatedWhen(typeof(Aggregate), 
            Aggregate.OneMethod,
            Expression.Constant(current),
            Expression.Constant(envelope));
        var func = Expression.Lambda<Func<Aggregate>>(exp).Compile();

        var agg = func();
        Assert.NotNull(agg);
        Assert.NotNull(agg.One);
        Assert.Null(agg.Two);
        Assert.True(envelope.Equals(agg.One));
    }

    [Fact]
    public void InvokeUpdatedWhen()
    {
        var currentParam = Expression.Parameter(typeof(Aggregate));
        var envParam = Expression.Parameter(typeof(EventEnvelope));

        var builder = new UpdatedWhenExpBuilder(Options);
        var exp = builder.BuildUpdateExpression(typeof(Aggregate), currentParam, envParam);
        var func = Expression.Lambda<Func<Aggregate, EventEnvelope, Aggregate>>(exp, currentParam, envParam).Compile();

        EventEnvelope New(object @event) => new EventEnvelope(Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            @event,
            new Metadata(Guid.NewGuid()));

        var one = New(new Updated1(Guid.NewGuid()));
        var two = New(new Updated2(Guid.NewGuid()));
        var three = New(new Updated3(Guid.NewGuid()));

        var cur = func(new Aggregate(null, new EventEnvelope<Updated2>(two)), one);

        Assert.NotNull(cur);
        Assert.NotNull(cur.One);
        Assert.NotNull(cur.Two);
        Assert.True(cur.One!.Equals(one));
        Assert.True(cur.Two!.Equals(two));
        
        cur = func(new Aggregate(new EventEnvelope<Updated1>(one), null), two);

        Assert.NotNull(cur);
        Assert.NotNull(cur.One);
        Assert.NotNull(cur.Two);
        Assert.True(cur.One!.Equals(one));
        Assert.True(cur.Two!.Equals(two));

        Assert.Throws<ArgumentException>(() => func(new Aggregate(null, null), New(three)));
    }

    private record Aggregate(EventEnvelope<Updated1>? One, EventEnvelope<Updated2>? Two)
    {
        public static Aggregate When(Aggregate current, EventEnvelope<Updated1> envelope) =>
            current with {One = envelope };

        public static Aggregate When(Aggregate current, EventEnvelope<Updated2> envelope) =>
            current with { Two = envelope };

        public static Aggregate When(EventEnvelope<Updated3> _) =>
            throw new Exception("This is a created when method");

        private  static MethodInfo GetMethod(Type eventType) => typeof(Aggregate)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(When) 
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType.GetGenericArguments()[0] == eventType);

        public static readonly MethodInfo OneMethod = GetMethod(typeof(Updated1));

        public static readonly MethodInfo TwoMethod = GetMethod(typeof(Updated2));

        public static readonly MethodInfo ThreeMethod = typeof(Aggregate)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.GetParameters().Length == 1);
    }

    private record Updated1(Guid Id);
    private record Updated2(Guid Id);
    private record Updated3(Guid Id);
    private record Metadata(Guid Id);

    private record EventEnvelope<T> : EventEnvelope<T, Metadata> where T : notnull
    {
        public EventEnvelope(EventEnvelope source) : base(source)
        {
        }
        
        public EventEnvelope(EventEnvelope<T> source) : base(source) {}
    }
}