using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PureES.Core.EventStore;
using PureES.Core.ExpBuilders.AggregateCmdHandlers;
using PureES.Core.Tests.Models;
using Xunit;
// ReSharper disable AccessToDisposedClosure

namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;

public class UpdateOnHandlerTests
{
    [Theory]
    [InlineData(nameof(Aggregate.UpdateOn))]
    [InlineData(nameof(Aggregate.UpdateOnAsync))]
    public void BuildHandler(string methodName)
    {
        var eventStore = new Mock<IEventStore>();
        var created = new EventEnvelope(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            Events.Created.New(),
            new Metadata());
        var cmd = Commands.Update.New();
        var metadata = new object();
        var expectedVersion = Rand.NextULong();
        var version = Rand.NextULong();
        var ct = new CancellationTokenSource().Token;

        eventStore.Setup(s => s.Load(cmd.Id.StreamId, expectedVersion, ct))
            .Returns(new []{created}.AsAsyncEnumerable())
            .Verifiable("Load not called");
        
        
        eventStore.Setup(s => s.Append(cmd.Id.StreamId,
                0, //Single event
                It.Is<UncommittedEvent>(e =>
                    e.Event is Events.Updated 
                    && ((Events.Updated) e.Event).Equals(cmd)
                    && ReferenceEquals(metadata, e.Metadata)),
                ct))
            .Returns(Task.FromResult(version))
            .Verifiable("EventStore.Append not called");
        
        using var sp = Services.Build(s => s
            .AddSingleton(eventStore.Object)
            .AddEventEnricher((c, e) =>
            {
                Assert.Equal(cmd, c);
                Assert.True(e is Events.Updated @event && @event.Equals(cmd));
                return metadata;
            })
            .AddOptimisticConcurrency(c =>
            {
                Assert.Equal(cmd, c);
                return expectedVersion;
            }));

        var builder = new UpdateOnHandlerExpBuilder(new CommandHandlerOptions());
        var exp = builder.BuildUpdateOnExpression(typeof(Aggregate),
            typeof(Aggregate).GetMethod(methodName, BindingFlags.Static | BindingFlags.Public)!,
            Expression.Constant(cmd),
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal(version, func().GetAwaiter().GetResult());
        eventStore.Verify();
    }
    
    [Theory]
    [InlineData(nameof(Aggregate.UpdateOnNull))]
    [InlineData(nameof(Aggregate.UpdateOnNullAsync))]
    public void BuildHandlerNull(string methodName)
    {
        var eventStore = new Mock<IEventStore>();
        var created = new EventEnvelope(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Rand.NextULong(),
            DateTime.UtcNow,
            Events.Created.New(),
            new Metadata());
        var cmd = Commands.Update.New();
        var expectedVersion = Rand.NextULong();
        var ct = new CancellationTokenSource().Token;

        var eventEnricher = new Mock<IEventEnricher>();

        eventStore.Setup(s => s.Load(cmd.Id.StreamId, expectedVersion, ct))
            .Returns(new []{created}.AsAsyncEnumerable())
            .Verifiable("Load not called");

        using var sp = Services.Build(s => s
            .AddSingleton(eventStore.Object)
            .AddSingleton(eventEnricher.Object)
            .AddOptimisticConcurrency(c =>
            {
                Assert.Equal(cmd, c);
                return expectedVersion;
            }));

        var builder = new UpdateOnHandlerExpBuilder(new CommandHandlerOptions());
        var exp = builder.BuildUpdateOnExpression(typeof(Aggregate),
            typeof(Aggregate).GetMethod(methodName, BindingFlags.Static | BindingFlags.Public)!,
            Expression.Constant(cmd),
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal((ulong)0, func().GetAwaiter().GetResult());

        eventStore.Verify(s => s.Append(cmd.Id.StreamId,
            created.StreamPosition, //Single event (from above)
            It.IsAny<UncommittedEvent>(),
            ct), 
            Times.Never);

        eventEnricher.Verify(e => e.GetMetadata(cmd, It.IsAny<object>(), ct), Times.Never);
    }
    
    private record Aggregate(EventEnvelope<Events.Created, Metadata> Created)
    {
        public static Aggregate When(EventEnvelope<Events.Created, Metadata> @event) => new (@event);

        public static Events.Updated UpdateOn(Aggregate aggregate, [Command] Commands.Update cmd)
        {
            Assert.NotNull(aggregate);
            Assert.NotNull(aggregate.Created);
            return new Events.Updated(cmd.Id, cmd.Value);
        }

        public static Task<Events.Updated> UpdateOnAsync(Aggregate aggregate, [Command] Commands.Update cmd)
        {
            Assert.NotNull(aggregate);
            Assert.NotNull(aggregate.Created);
            return Task.FromResult(new Events.Updated(cmd.Id, cmd.Value));
        }
        
        public static object? UpdateOnNull(Aggregate aggregate, [Command] Commands.Update cmd)
        {
            Assert.NotNull(aggregate);
            Assert.NotNull(aggregate.Created);
            return null;
        }
        
        public static Task<object?> UpdateOnNullAsync(Aggregate aggregate, [Command] Commands.Update cmd)
        {
            Assert.NotNull(aggregate);
            Assert.NotNull(aggregate.Created);
            return Task.FromResult<object?>(null);
        }
    }

    private record Metadata;
}