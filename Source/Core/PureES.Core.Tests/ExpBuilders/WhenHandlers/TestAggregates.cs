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
    public static CommandHandlerBuilderOptions Options => new()
    {
        IsStronglyTypedEventEnvelope = type =>
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

    public static IEnumerable<Type> GetAggregateTypes() =>
        new[]
        {
            typeof(Aggregate),
            typeof(AggregateAsync),
            typeof(AggregateValueTaskAsync),
            typeof(AggregateCustomEnvelope),
            typeof(AggregateAsyncCustomEnvelope),
            typeof(AggregateValueTaskAsyncCustomEnvelope)
        };

    public static IEnumerable<object[]> GetAggregateAndCreatedEventTypes() => GetAggregateTypes()
        .SelectMany(t => new[]
        {
            new object[] {t, typeof(Created1)},
            new object[] {t, typeof(Created2)}
        });

    public static IEnumerable<object[]> GetAggregateAndUpdatedEventTypes() => GetAggregateTypes()
        .SelectMany(t => new[]
        {
            new object[] {t, typeof(Updated1)},
            new object[] {t, typeof(Updated2)}
        });

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
            .Single(p => p.PropertyType.GetGenericArguments().Length > 0 && 
                         p.PropertyType.GetGenericArguments()[0] == typeof(TEvent))
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
    
    public static void AssertEqual(object aggregate, EventEnvelope envelope)
    {
        var assertEqualMethod = typeof(TestAggregates).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(AssertEqual) && m.GetGenericArguments().Length == 1);
        assertEqualMethod.MakeGenericMethod(envelope.Event.GetType())
            .Invoke(null, new[] {aggregate, envelope});
    }
    
    public static void AssertWhen(object aggregate, EventEnvelope last)
    {
        Assert.NotEqual(DateTime.MaxValue, last.Timestamp);
        Assert.NotEqual(DateTime.MinValue, last.Timestamp);
        Assert.NotEqual(default, last.Timestamp);
        
        Assert.NotEqual(ulong.MaxValue, last.StreamPosition);
        
        var streamPositionProp = aggregate.GetType()
                       .GetProperty(nameof(Aggregate.StreamPosition), BindingFlags.Public | BindingFlags.Instance)
                   ?? throw new Exception($"Unable to get StreamPosition property from type {aggregate.GetType()}");
        var lastUpdatedOnProp = aggregate.GetType()
                       .GetProperty(nameof(Aggregate.LastUpdatedOn), BindingFlags.Public | BindingFlags.Instance)
                   ?? throw new Exception($"Unable to get last updated on property from type {aggregate.GetType()}");
        var streamPosition = streamPositionProp.GetValue(aggregate);
        var lastUpdated = lastUpdatedOnProp.GetValue(aggregate);
        Assert.NotNull(streamPosition);
        Assert.NotNull(lastUpdated);

        Assert.NotEqual(ulong.MaxValue, (ulong) streamPosition!);
        Assert.Equal(last.StreamPosition, (ulong) streamPosition);
        Assert.Equal(last.Timestamp, (DateTime) lastUpdated!);
    }

    public static Lazy<object> NewEvent<T>() => new (() => 
    {
        var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(Guid));
        return constructor.Invoke(new object?[] {Guid.NewGuid()})!;
    }, true);

    public static T NewAggregate<T>()
    {
        var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance).First();
        //Null for all parameters
        return (T) constructor.Invoke(constructor.GetParameters().Select(_ => (object?) null).ToArray());
    }
}

public record Aggregate(EventEnvelope<Created1, Metadata>? Created1,
    EventEnvelope<Created2, Metadata>? Created2,
    EventEnvelope<Updated1, Metadata>? Updated1,
    EventEnvelope<Updated2, Metadata>? Updated2,
    DateTime LastUpdatedOn,
    ulong StreamPosition)
{
    public static Aggregate When(EventEnvelope<Created1, Metadata> envelope, CancellationToken _) =>
        new(envelope, null, null, null, DateTime.MaxValue, ulong.MaxValue);

    public static Aggregate When(EventEnvelope<Created2, Metadata> envelope,
        [FromServices] AggregateService svc,
        CancellationToken _)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Created2, Metadata>(svc.Event!));
        return new Aggregate(null, envelope, null, null, DateTime.MaxValue, ulong.MaxValue);
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

    public static Aggregate When(Aggregate current,
        EventEnvelope envelope,
        [FromServices] AggregateService svc,
        CancellationToken _)
        => current with
        {
            StreamPosition = envelope.StreamPosition, 
            LastUpdatedOn = envelope.Timestamp
        };
}

public record AggregateAsync(EventEnvelope<Created1, Metadata>? Created1,
    EventEnvelope<Created2, Metadata>? Created2,
    EventEnvelope<Updated1, Metadata>? Updated1,
    EventEnvelope<Updated2, Metadata>? Updated2,
    DateTime LastUpdatedOn,
    ulong StreamPosition)
{
    public static Task<AggregateAsync> When(EventEnvelope<Created1, Metadata> envelope, CancellationToken _) =>
        Task.FromResult(new AggregateAsync(envelope, null, null, null, DateTime.MaxValue, ulong.MaxValue));

    public static Task<AggregateAsync> When(EventEnvelope<Created2, Metadata> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Created2, Metadata>(svc.Event!));
        return Task.FromResult(new AggregateAsync(null, envelope, null, null, DateTime.MaxValue, ulong.MaxValue));
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

    public static Task<AggregateAsync> When(AggregateAsync current,
        EventEnvelope envelope,
        [FromServices] AggregateService svc,
        CancellationToken _)
        => Task.FromResult(current with
        {
            StreamPosition = envelope.StreamPosition,
            LastUpdatedOn = envelope.Timestamp
        });
}

public record AggregateValueTaskAsync(EventEnvelope<Created1, Metadata>? Created1,
    EventEnvelope<Created2, Metadata>? Created2,
    EventEnvelope<Updated1, Metadata>? Updated1,
    EventEnvelope<Updated2, Metadata>? Updated2,
    DateTime LastUpdatedOn,
    ulong StreamPosition)
{
    public static ValueTask<AggregateValueTaskAsync> When(EventEnvelope<Created1, Metadata> envelope,
        CancellationToken _) =>
        ValueTask.FromResult(new AggregateValueTaskAsync(envelope, null, null, null, DateTime.MaxValue, ulong.MaxValue));

    public static ValueTask<AggregateValueTaskAsync> When(EventEnvelope<Created2, Metadata> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, new EventEnvelope<Created2, Metadata>(svc.Event!));
        return ValueTask.FromResult(new AggregateValueTaskAsync(null, envelope, null, null, DateTime.MaxValue, ulong.MaxValue));
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

    public static ValueTask<AggregateValueTaskAsync> When(AggregateValueTaskAsync current,
        EventEnvelope envelope,
        [FromServices] AggregateService svc,
        CancellationToken _)
        => ValueTask.FromResult(current with
        {
            StreamPosition = envelope.StreamPosition,
            LastUpdatedOn = envelope.Timestamp
        });
}

public record AggregateCustomEnvelope(EventEnvelope<Created1>? Created1,
    EventEnvelope<Created2>? Created2,
    EventEnvelope<Updated1>? Updated1,
    EventEnvelope<Updated2>? Updated2,
    DateTime LastUpdatedOn,
    ulong StreamPosition)
{
    public static AggregateCustomEnvelope When(EventEnvelope<Created1> envelope, CancellationToken _) =>
        new(envelope, null, null, null, DateTime.MaxValue, ulong.MaxValue);

    public static AggregateCustomEnvelope When(EventEnvelope<Created2> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, svc.Event!);
        return new AggregateCustomEnvelope(null, envelope, null, null, DateTime.MaxValue, ulong.MaxValue);
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

    public static AggregateCustomEnvelope When(AggregateCustomEnvelope current,
        EventEnvelope envelope,
        [FromServices] AggregateService svc,
        CancellationToken _)
        => current with
        {
            StreamPosition = envelope.StreamPosition,
            LastUpdatedOn = envelope.Timestamp
        };
}

public record AggregateAsyncCustomEnvelope(EventEnvelope<Created1>? Created1,
    EventEnvelope<Created2>? Created2,
    EventEnvelope<Updated1>? Updated1,
    EventEnvelope<Updated2>? Updated2,
    DateTime LastUpdatedOn,
    ulong StreamPosition)
{
    public static Task<AggregateAsyncCustomEnvelope> When(EventEnvelope<Created1> envelope, CancellationToken _) =>
        Task.FromResult(new AggregateAsyncCustomEnvelope(envelope, null, null, null, DateTime.MaxValue, ulong.MaxValue));

    public static Task<AggregateAsyncCustomEnvelope> When(EventEnvelope<Created2> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, svc.Event!);
        return Task.FromResult(new AggregateAsyncCustomEnvelope(null, envelope, null, null, DateTime.MaxValue, ulong.MaxValue));
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

    public static Task<AggregateAsyncCustomEnvelope> When(AggregateAsyncCustomEnvelope current,
        EventEnvelope envelope,
        [FromServices] AggregateService svc,
        CancellationToken _)
        => Task.FromResult(current with
        {
            StreamPosition = envelope.StreamPosition,
            LastUpdatedOn = envelope.Timestamp
        });
}

public record AggregateValueTaskAsyncCustomEnvelope(EventEnvelope<Created1>? Created1,
    EventEnvelope<Created2>? Created2,
    EventEnvelope<Updated1>? Updated1,
    EventEnvelope<Updated2>? Updated2,
    DateTime LastUpdatedOn,
    ulong StreamPosition)
{
    public static ValueTask<AggregateValueTaskAsyncCustomEnvelope> When(EventEnvelope<Created1> envelope,
        CancellationToken _) =>
        ValueTask.FromResult(new AggregateValueTaskAsyncCustomEnvelope(envelope, null, null, null, DateTime.MaxValue, ulong.MaxValue));

    public static ValueTask<AggregateValueTaskAsyncCustomEnvelope> When(EventEnvelope<Created2> envelope,
        [FromServices] AggregateService svc)
    {
        if (svc.Event != null)
            Assert.Equal(envelope, svc.Event);
        return ValueTask.FromResult(new AggregateValueTaskAsyncCustomEnvelope(null, envelope, null, null, DateTime.MaxValue, ulong.MaxValue));
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

    public static ValueTask<AggregateValueTaskAsyncCustomEnvelope> When(AggregateValueTaskAsyncCustomEnvelope current,
        EventEnvelope envelope,
        [FromServices] AggregateService svc,
        CancellationToken _)
        => ValueTask.FromResult(current with
        {
            StreamPosition = envelope.StreamPosition,
            LastUpdatedOn = envelope.Timestamp
        });
}

public record Created1(Guid Id);

public record Created2(Guid Id);

public record Updated1(Guid Id);

public record Updated2(Guid Id);

public record Metadata(Guid Id)
{
    public static Lazy<object?> New() => new(() => new Metadata(Guid.NewGuid()), true);
}

public class EventEnvelope<T> : EventEnvelope<T, Metadata> where T : notnull
{
    public EventEnvelope(EventEnvelope source) : base(source)
    {
    }

    public EventEnvelope(EventEnvelope<T, Metadata> source) : base(source)
    {
    }

    public static implicit operator EventEnvelope<T>(EventEnvelope e) => new(e);
}

public class AggregateService
{
    public EventEnvelope? Event { get; set; }
}