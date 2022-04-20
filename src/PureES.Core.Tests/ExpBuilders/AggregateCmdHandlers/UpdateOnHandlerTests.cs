using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PureES.Core.ExpBuilders.AggregateCmdHandlers;
using PureES.Core.Tests.Models;
using Xunit;

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
            .Returns(Task.FromResult(ImmutableArray.Create<EventEnvelope>(created)))
            .Verifiable("Load not called");
        
        eventStore.Setup(s => s.Append(cmd.Id.StreamId,
                1, //Single event (from above)
                It.Is<UncommittedEvent>(e =>
                    e.Event is Events.Updated 
                    && ((Events.Updated) e.Event).Equals(cmd)
                    && ReferenceEquals(metadata, e.Metadata)),
                ct))
            .Returns(Task.FromResult(version))
            .Verifiable("EventStore.Append not called");
        
        using var sp = Services.Build(s => s
            .AddSingleton(eventStore.Object));

        var builder = new UpdateOnHandlerExpBuilder(new CommandHandlerOptions()
        {
            GetMetadata = (c, @event) =>
            {
                Assert.Equal(cmd, c);
                Assert.True(@event is Events.Updated e && e.Equals(cmd));
                return metadata;
            },
            GetExpectedVersion = (c, provider) =>
            {
                // ReSharper disable once AccessToDisposedClosure
                Assert.Same(sp, provider);
                Assert.Equal(cmd, c);
                return expectedVersion;
            }
        });
        var exp = builder.BuildUpdateOnExpression(typeof(Aggregate),
            typeof(Aggregate).GetMethod(methodName, BindingFlags.Static | BindingFlags.Public)!,
            Expression.Constant(cmd),
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(ct, typeof(CancellationToken?)));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal(version, func().GetAwaiter().GetResult());
        eventStore.Verify();
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
    }

    public record Metadata;
}