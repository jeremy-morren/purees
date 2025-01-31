using System;
using PureES.Tests.Models;
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

    private static EventEnvelope NewEnvelope() => new (
        Guid.NewGuid().ToString(),
        Rand.Nextuint(),
        DateTime.UtcNow,
        Events.Created.New(),
        Object);

    private static readonly object Object = new();
}