using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PureES.Core.ExpBuilders;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders;

public class InvokeEventStoreExpBuilderTests
{
    #region Create
    
    [Fact]
    public void CreateMultiple()
    {
        var list = Enumerable.Empty<UncommittedEvent>();
        var streamId = Guid.NewGuid().ToString();
        var ct = new CancellationTokenSource().Token;
        var ret = Rand.NextULong();
        
        var eventStore = new Mock<IEventStore>();
        eventStore.Setup(s => s.Create(streamId, list, ct))
            .Returns(Task.FromResult(ret))
            .Verifiable();

        var exp = new InvokeEventStoreExpBuilder(new CommandHandlerOptions())
            .CreateMultiple(Expression.Constant(eventStore.Object),
                Expression.Constant(streamId),
                Expression.Constant(list),
                Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal(ret, func().GetAwaiter().GetResult());
        eventStore.Verify();
    }
    
    [Fact]
    public void CreateSingle()
    {
        var @event = new UncommittedEvent(Guid.Empty, new object(), null);
        var streamId = Guid.NewGuid().ToString();
        var ct = new CancellationTokenSource().Token;
        var ret = Rand.NextULong();
        
        var eventStore = new Mock<IEventStore>();
        eventStore.Setup(s => s.Create(streamId, @event, ct))
            .Returns(Task.FromResult(ret))
            .Verifiable();

        var exp = new InvokeEventStoreExpBuilder(new CommandHandlerOptions())
            .CreateSingle(Expression.Constant(eventStore.Object),
                Expression.Constant(streamId),
                Expression.Constant(@event),
                Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal(ret, func().GetAwaiter().GetResult());
        eventStore.Verify();
    }
    
    #endregion
    
    #region Append

    [Fact]
    public void AppendMultiple()
    {
        var streamId = Guid.NewGuid().ToString();
        var expectedVersion = Rand.NextULong();
        var events = Enumerable.Empty<UncommittedEvent>();
        var ct = new CancellationTokenSource().Token;
        var ret = Rand.NextULong();
        
        var eventStore = new Mock<IEventStore>();
        eventStore.Setup(s => s.Append(streamId, expectedVersion, events, ct))
            .Returns(Task.FromResult(ret))
            .Verifiable();
        
        var exp = new InvokeEventStoreExpBuilder(new CommandHandlerOptions())
            .AppendMultiple(Expression.Constant(eventStore.Object),
                Expression.Constant(streamId),
                Expression.Constant(expectedVersion),
                Expression.Constant(events),
                Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal(ret, func().GetAwaiter().GetResult());
        eventStore.Verify();
    }
    
    [Fact]
    public void AppendSingle()
    {
        var @event = new UncommittedEvent(Guid.Empty, new object(), null);
        var streamId = Guid.NewGuid().ToString();
        var expectedVersion = Rand.NextULong();
        var ct = new CancellationTokenSource().Token;
        var ret = Rand.NextULong();
        
        var eventStore = new Mock<IEventStore>();
        eventStore.Setup(s => s.Append(streamId, expectedVersion, @event, ct))
            .Returns(Task.FromResult(ret))
            .Verifiable();
        
        var exp = new InvokeEventStoreExpBuilder(new CommandHandlerOptions())
            .AppendSingle(Expression.Constant(eventStore.Object),
                Expression.Constant(streamId),
                Expression.Constant(expectedVersion),
                Expression.Constant(@event),
                Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal(ret, func().GetAwaiter().GetResult());
        eventStore.Verify();
    }
    
    #endregion
    
}