using System;
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

namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;

public class CreateOnHandlerTests
{
    [Theory]
    [InlineData(nameof(TestAggregate.Create))]
    [InlineData(nameof(TestAggregate.CreateAsync))]
    [InlineData(nameof(TestAggregate.CreateValueTaskAsync))]
    public void BuildHandler(string methodName)
    {
        var eventStore = new Mock<IEventStore>();
        var cmd = Commands.Create.New();
        var metadata = new object();
        var version = Rand.NextULong();
        var ct = new CancellationTokenSource().Token;
        eventStore.Setup(s => s.Create(cmd.Id.StreamId,
                It.Is<UncommittedEvent>(e =>
                    e.Event is Events.Created && ((Events.Created) e.Event).Equals(cmd)
                                              && ReferenceEquals(metadata, e.Metadata)),
                ct))
            .Returns(Task.FromResult(version))
            .Verifiable("EventStore.Create not called");

        using var sp = Services.Build(s => s
            .AddSingleton(eventStore.Object)
            .AddEventEnricher((c, e) =>
            {
                Assert.Equal(cmd, c);
                Assert.True(e is Events.Created @event && @event.Equals(cmd));
                return metadata;
            }));

        var builder = new CreateOnHandlerExpBuilder(new CommandHandlerBuilderOptions());
        var exp = builder.BuildCreateOnExpression(typeof(TestAggregate),
            typeof(TestAggregate).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!,
            Expression.Constant(cmd),
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal(version, func().GetAwaiter().GetResult());
        eventStore.Verify();
    }

    [Theory]
    [InlineData(nameof(TestAggregate.CreateWithResult))]
    [InlineData(nameof(TestAggregate.CreateWithResultAsync))]
    [InlineData(nameof(TestAggregate.CreateWithDerivedResult))]
    [InlineData(nameof(TestAggregate.CreateWithDerivedResultAsync))]
    [InlineData(nameof(TestAggregate.CreateWithDerivedResultValueTaskAsync))]
    public void BuildHandlerWithResult(string methodName)
    {
        var eventStore = new Mock<IEventStore>();
        var cmd = Commands.Create.New();
        var metadata = new object();
        var version = Rand.NextULong();
        var ct = new CancellationTokenSource().Token;
        eventStore.Setup(s => s.Create(cmd.Id.StreamId,
                It.Is<UncommittedEvent>(e =>
                    e.Event is Events.Created && ((Events.Created) e.Event).Equals(cmd)
                                              && ReferenceEquals(metadata, e.Metadata)),
                ct))
            .Returns(Task.FromResult(version))
            .Verifiable("EventStore.Create not called");

        using var sp = Services.Build(s => s
            .AddSingleton(eventStore.Object)
            .AddEventEnricher((c, e) =>
            {
                Assert.Equal(cmd, c);
                Assert.True(e is Events.Created @event && @event.Equals(cmd));
                return metadata;
            }));

        var builder = new CreateOnHandlerExpBuilder(new CommandHandlerBuilderOptions());
        var exp = builder.BuildCreateOnExpression(typeof(TestAggregate),
            typeof(TestAggregate).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!,
            Expression.Constant(cmd),
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<Result>>>(exp).Compile();

        Assert.Equal(cmd.Id, func().GetAwaiter().GetResult().Id);
        eventStore.Verify();
    }

    private record TestAggregate
    {
        public static Events.Created Create([Command] Commands.Create cmd) => new(cmd.Id, cmd.Value);

        public static Task<Events.Created> CreateAsync([Command] Commands.Create cmd) => Task.FromResult(Create(cmd));

        public static ValueTask<Events.Created> CreateValueTaskAsync([Command] Commands.Create cmd) =>
            ValueTask.FromResult(Create(cmd));

        public static CommandResult<Events.Created, Result> CreateWithResult([Command] Commands.Create cmd)
            => new(new Events.Created(cmd.Id, cmd.Value), new Result(cmd.Id));

        public static Task<CommandResult<Events.Created, Result>> CreateWithResultAsync([Command] Commands.Create cmd)
            => Task.FromResult(CreateWithResult(cmd));

        public static ValueTask<CommandResult<Events.Created, Result>> CreateWithResultValueTaskAsync(
            [Command] Commands.Create cmd)
            => ValueTask.FromResult(CreateWithResult(cmd));

        public static CommandResult CreateWithDerivedResult([Command] Commands.Create cmd)
            => new(new Events.Created(cmd.Id, cmd.Value), new Result(cmd.Id));

        public static Task<CommandResult> CreateWithDerivedResultAsync([Command] Commands.Create cmd)
            => Task.FromResult(CreateWithDerivedResult(cmd));

        public static ValueTask<CommandResult> CreateWithDerivedResultValueTaskAsync([Command] Commands.Create cmd)
            => ValueTask.FromResult(CreateWithDerivedResult(cmd));
    }

    private record Result(TestAggregateId Id);

    private record CommandResult(Events.Created Event, Result Result)
        : CommandResult<Events.Created, Result>(Event, Result);
}