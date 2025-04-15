using System;
using PureES.Tests.Models;
using Shouldly;
using Xunit;

namespace PureES.Tests;

public class EventEnvelopeTests
{
    [Fact]
    public void CopiedShouldBeEqual()
    {
        var env = NewEnvelope();
        Assert.Equal(env, new EventEnvelope(env));
        Assert.True(new EventEnvelope<Events.Created, object>(env).Equals(env));
        Assert.Equal(new EventEnvelope<Events.Created, object>(env), 
            new EventEnvelope<Events.Created, object>(env));
    }

    [Fact]
    public void NonUtcTimestampShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new EventEnvelope(
            Guid.NewGuid().ToString(),
            Rand.Nextuint(),
            DateTime.Now,
            Events.Created.New(),
            Object));
    }

    [Fact]
    public void CastShouldHandleInheritedTypes()
    {
        var env = new EventEnvelope(
            Guid.NewGuid().ToString(),
            Rand.Nextuint(),
            DateTime.UtcNow,
            new EventDerived(),
            Object);
        var casted = env.Cast<EventBase, object>().ShouldNotBeNull();
        casted.Cast<EventDerived, object>().ShouldNotBeNull();
    }

    private static EventEnvelope NewEnvelope() => new (
        Guid.NewGuid().ToString(),
        Rand.Nextuint(),
        DateTime.UtcNow,
        Events.Created.New(),
        Object);

    private static readonly object Object = new();

    private class EventBase {}

    private class EventDerived : EventBase {}
}