using System;
using System.Linq.Expressions;
using PureES.Core.EventStore;
using PureES.Core.ExpBuilders;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders;

public class NewUncommittedEventExpBuilderTests
{
    [Fact]
    public void CreateUncommittedEvent()
    {
        var eventId = Expression.Constant(Guid.NewGuid(), typeof(Guid));
        var @event = Expression.Constant(new object(), typeof(object));
        var metadata = Expression.Constant(new object(), typeof(object));
        var exp = new NewUncommittedEventExpBuilder(new CommandHandlerBuilderOptions())
            .New(eventId, @event, metadata);
        var func = Expression.Lambda<Func<UncommittedEvent>>(exp).Compile();
        var @new = func();
        Assert.Equal((Guid) eventId.Value!, @new.EventId);
        Assert.Same(@event.Value, @new.Event);
        Assert.Same(metadata.Value, @new.Metadata);
    }

    [Fact]
    public void CreateUncommittedEvent_WithComplexTypes()
    {
        var eventId = Expression.Constant(Guid.NewGuid(), typeof(Guid));
        var @event = Expression.Constant(0, typeof(int));
        var metadata = Expression.Constant(string.Empty, typeof(string));
        var exp = new NewUncommittedEventExpBuilder(new CommandHandlerBuilderOptions())
            .New(eventId, @event, metadata);
        var func = Expression.Lambda<Func<UncommittedEvent>>(exp).Compile();
        var @new = func();
        Assert.Equal((Guid) eventId.Value!, @new.EventId);
        Assert.Equal(@event.Value, @new.Event);
        Assert.Equal(metadata.Value, @new.Metadata);
    }

    [Fact]
    public void CreateUncommittedEvent_WithNewGuid()
    {
        var @event = Expression.Constant(new object(), typeof(object));
        var metadata = Expression.Constant(new object(), typeof(object));
        var exp = new NewUncommittedEventExpBuilder(new CommandHandlerBuilderOptions())
            .New(@event, metadata);
        var func = Expression.Lambda<Func<UncommittedEvent>>(exp).Compile();
        var @new = func();
        Assert.NotEqual(Guid.Empty, @new.EventId);
    }
}