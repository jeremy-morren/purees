using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Xunit;

// ReSharper disable UnusedMember.Global
// ReSharper disable NotAccessedPositionalProperty.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedParameter.Global
// ReSharper disable SuggestBaseTypeForParameter
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
// ReSharper disable MemberCanBePrivate.Local
// ReSharper disable NotAccessedPositionalProperty.Local

namespace PureES.Core.Tests.ExpBuilders.WhenHandlers;

public static class TestAggregates
{
    public static IEnumerable<Type> GetAggregateTypes() =>
        new[]
        {
            typeof(Aggregate),
            typeof(AggregateAsync),
            typeof(AggregateValueTaskAsync),
            typeof(AggregateCustomEnvelope),
            typeof(AggregateAsyncCustomEnvelope),
            typeof(AggregateValueTaskAsyncCustomEnvelope),
        };

    public static IEnumerable<object[]> GetAggregateAndCreatedEventTypes() => GetAggregateTypes()
        .SelectMany(t => new []
        {
            new object[] {t, typeof(Created1)},
            new object[] {t, typeof(Created2)}
        });
    
    public static IEnumerable<object[]> GetAggregateAndUpdatedEventTypes() => GetAggregateTypes()
        .SelectMany(t => new []
        {
            new object[] {t, typeof(Updated1)},
            new object[] {t, typeof(Updated2)}
        });
    
    public static CommandHandlerBuilderOptions Options => new ()
    {
        IsEventEnvelope = type =>
        {
            return type.GetGenericArguments().Length switch
            {
                1 => typeof(EventEnvelope<>).MakeGenericType(type.GetGenericArguments()) == type,
                2 => typeof(EventEnvelope<,>).MakeGenericType(type.GetGenericArguments()) == type,
                _ => false
            };
        },
        GetEventType = envelope => envelope.GetGenericArguments()[0]
    };
    
    public static MethodInfo GetMethod(Type aggregateType, Type eventType) => aggregateType
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == "When"
                     && m.GetParameters().Any(p =>
                         p.ParameterType.GetGenericArguments().Length > 0
                         //TEvent is always first envelope generic argument
                         && p.ParameterType.GetGenericArguments()[0] == eventType));

    public static EventEnvelope<TEvent, Metadata>? GetEnvelope<TEvent>(object aggregate)
        where TEvent : notnull
    {
        var env = (EventEnvelope<TEvent, Metadata>?) aggregate
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(p => p.PropertyType.GetGenericArguments()[0] == typeof(TEvent))
            .GetValue(aggregate);
        return env != (EventEnvelope?) null ? new EventEnvelope<TEvent, Metadata>(env) : null;
    }
    
    public static void AssertEqual<TEvent>(object aggregate, EventEnvelope envelope)
        where TEvent : notnull
    {
        var actual = GetEnvelope<TEvent>(aggregate);
        Assert.NotNull(actual);
        Assert.Equal(new EventEnvelope<TEvent, Metadata>(envelope), actual);
    }

    public static T NewEvent<T>()
    {
        var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(Guid));
        return (T) constructor.Invoke(new object?[] {Guid.NewGuid()});
    }

    public static T NewAggregate<T>()
    {
        var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance).First();
        //Null for all parameters
        return (T)constructor.Invoke(constructor.GetParameters().Select(_ => (object?) null).ToArray());
    }
}

public record Aggregate(EventEnvelope<Created1, Metadata>? Created1, 
    EventEnvelope<Created2, Metadata>? Created2,
    EventEnvelope<Updated1, Metadata>? Updated1,
    EventEnvelope<Updated2, Metadata>? Updated2)
{
    public static Aggregate When(EventEnvelope<Created1, Metadata> envelope, CancellationToken _) =>
        new(envelope, null, null, null);

    public static Aggregate When(EventEnvelope<Created2, Metadata> envelope,
        [FromServices] AggregateService svc,
        CancellationToken _)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Created2, Metadata>(svc.Event!));
        return new Aggregate(null, envelope, null, null);
    }

    public static Aggregate When(Aggregate current, EventEnvelope<Updated1, Metadata> envelope, CancellationToken _)
        => current with {Updated1 = envelope};
    
    public static Aggregate When(Aggregate current, 
        EventEnvelope<Updated2, Metadata> envelope, 
        [FromServices] AggregateService svc,
        CancellationToken _)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Updated2, Metadata>(svc.Event!));
        return current with {Updated2 = envelope};
    }
}

public  record AggregateAsync(EventEnvelope<Created1, Metadata>? Created1, 
    EventEnvelope<Created2, Metadata>? Created2,
    EventEnvelope<Updated1, Metadata>? Updated1,
    EventEnvelope<Updated2, Metadata>? Updated2)
{
    public static Task<AggregateAsync> When(EventEnvelope<Created1, Metadata> envelope, CancellationToken _) =>
        Task.FromResult(new AggregateAsync(envelope, null, null, null));

    public static Task<AggregateAsync> When(EventEnvelope<Created2, Metadata> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Created2, Metadata>(svc.Event!));
        return Task.FromResult(new AggregateAsync(null, envelope, null, null));
    }

    public static Task<AggregateAsync> When(AggregateAsync current, 
        EventEnvelope<Updated1, Metadata> envelope,
        CancellationToken _)
        => Task.FromResult(current with {Updated1 = envelope});
    
    public static Task<AggregateAsync> When(AggregateAsync current, 
        EventEnvelope<Updated2, Metadata> envelope, 
        [FromServices] AggregateService svc,
        CancellationToken _)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Updated2, Metadata>(svc.Event!));
        return Task.FromResult(current with {Updated2 = envelope});
    }
}

public record AggregateValueTaskAsync(EventEnvelope<Created1, Metadata>? Created1, 
    EventEnvelope<Created2, Metadata>? Created2,
    EventEnvelope<Updated1, Metadata>? Updated1,
    EventEnvelope<Updated2, Metadata>? Updated2)
{
    public static ValueTask<AggregateValueTaskAsync> When(EventEnvelope<Created1, Metadata> envelope, CancellationToken _) =>
        ValueTask.FromResult(new AggregateValueTaskAsync(envelope, null, null, null));

    public static ValueTask<AggregateValueTaskAsync> When(EventEnvelope<Created2, Metadata> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Created2, Metadata>(svc.Event!));
        return ValueTask.FromResult(new AggregateValueTaskAsync(null, envelope, null, null));
    }

    public static ValueTask<AggregateValueTaskAsync> When(AggregateValueTaskAsync current, 
        EventEnvelope<Updated1, Metadata> envelope,
        CancellationToken _)
        => ValueTask.FromResult(current with {Updated1 = envelope});
    
    public static ValueTask<AggregateValueTaskAsync> When(AggregateValueTaskAsync current, 
        EventEnvelope<Updated2, Metadata> envelope, 
        [FromServices] AggregateService svc,
        CancellationToken _)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Updated2, Metadata>(svc.Event!));
        return ValueTask.FromResult(current with {Updated2 = envelope});
    }
}

public record AggregateCustomEnvelope(EventEnvelope<Created1>? Created1,
    EventEnvelope<Created2>? Created2,
    EventEnvelope<Updated1>? Updated1,
    EventEnvelope<Updated2>? Updated2)
{
    public static AggregateCustomEnvelope When(EventEnvelope<Created1> envelope, CancellationToken _) =>
        new(envelope, null, null, null);

    public static AggregateCustomEnvelope When(EventEnvelope<Created2> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, svc.Event!);
        return new AggregateCustomEnvelope(null, envelope, null, null);
    }

    public static AggregateCustomEnvelope When(AggregateCustomEnvelope current,
        EventEnvelope<Updated1> envelope, 
        CancellationToken _)
        => current with {Updated1 = envelope};
    
    public static AggregateCustomEnvelope When(AggregateCustomEnvelope current, 
        EventEnvelope<Updated2> envelope, 
        [FromServices] AggregateService svc,
        CancellationToken _)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Updated2>(svc.Event!));
        return current with {Updated2 = envelope};
    }
}

public record AggregateAsyncCustomEnvelope(EventEnvelope<Created1>? Created1,
    EventEnvelope<Created2>? Created2,
    EventEnvelope<Updated1>? Updated1,
    EventEnvelope<Updated2>? Updated2)
{
    public static Task<AggregateAsyncCustomEnvelope> When(EventEnvelope<Created1> envelope, CancellationToken _) =>
        Task.FromResult(new AggregateAsyncCustomEnvelope(envelope, null, null, null));

    public static Task<AggregateAsyncCustomEnvelope> When(EventEnvelope<Created2> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, svc.Event!);
        return Task.FromResult(new AggregateAsyncCustomEnvelope(null, envelope, null, null));
    }

    public static Task<AggregateAsyncCustomEnvelope> When(AggregateAsyncCustomEnvelope current, 
        EventEnvelope<Updated1> envelope,
        CancellationToken _)
        => Task.FromResult(current with {Updated1 = envelope});
    
    public static Task<AggregateAsyncCustomEnvelope> When(AggregateAsyncCustomEnvelope current, 
        EventEnvelope<Updated2> envelope, 
        [FromServices] AggregateService svc,
        CancellationToken _)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Updated2>(svc.Event!));
        return Task.FromResult(current with {Updated2 = envelope});
    }
}

public record AggregateValueTaskAsyncCustomEnvelope(EventEnvelope<Created1>? Created1,
    EventEnvelope<Created2>? Created2,
    EventEnvelope<Updated1>? Updated1,
    EventEnvelope<Updated2>? Updated2)
{
    public static ValueTask<AggregateValueTaskAsyncCustomEnvelope> When(EventEnvelope<Created1> envelope, CancellationToken _) =>
        ValueTask.FromResult(new AggregateValueTaskAsyncCustomEnvelope(envelope, null, null, null));
    
    public static ValueTask<AggregateValueTaskAsyncCustomEnvelope> When(EventEnvelope<Created2> envelope, 
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, svc.Event);
        return ValueTask.FromResult(new AggregateValueTaskAsyncCustomEnvelope(null, envelope, null, null));
    }

    public static ValueTask<AggregateValueTaskAsyncCustomEnvelope> When(AggregateValueTaskAsyncCustomEnvelope current, 
        EventEnvelope<Updated1> envelope,
        CancellationToken _)
        => ValueTask.FromResult(current with {Updated1 = envelope});
    
    public static ValueTask<AggregateValueTaskAsyncCustomEnvelope> When(AggregateValueTaskAsyncCustomEnvelope current, 
        EventEnvelope<Updated2> envelope, 
        [FromServices] AggregateService svc,
        CancellationToken _)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Updated2>(svc.Event));
        return ValueTask.FromResult(current with {Updated2 = envelope});
    }
}

public record Created1(Guid Id);
public record Created2(Guid Id);

public record Updated1(Guid Id);
public record Updated2(Guid Id);

public record Metadata(Guid Id);

public record EventEnvelope<T> : EventEnvelope<T, Metadata> where T : notnull
{
    public EventEnvelope(EventEnvelope source) : base(source)
    {
    }
    
    public EventEnvelope(EventEnvelope<T> source) : base(source) {}

    public static implicit operator EventEnvelope<T>(EventEnvelope e) => new(e);
}

public class AggregateService
{
    public EventEnvelope? Event { get; set; }
}