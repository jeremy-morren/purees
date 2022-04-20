using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PureES.Core.ExpBuilders.AggregateCmdHandlers;
using PureES.Core.Tests.Models;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;

public class CreateOnHandlerTests
{
    [Theory]
    [InlineData(nameof(TestAggregate.Create))]
    [InlineData(nameof(TestAggregate.CreateAsync))]
    public void BuildHandler_Sync(string methodName)
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
            .AddSingleton(eventStore.Object));

        var builder = new CreateOnHandlerExpBuilder(new CommandHandlerOptions()
        {
            GetMetadata = (c, @event) =>
            {
                Assert.Equal(cmd, c);
                Assert.True(@event is Events.Created e && e.Equals(cmd));
                return metadata;
            }
        });
        var exp = builder.BuildCreateOnExpression(typeof(TestAggregate),
            typeof(TestAggregate).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!,
            Expression.Constant(cmd),
            Expression.Constant(sp, typeof(IServiceProvider)),
            Expression.Constant(ct, typeof(CancellationToken?)));
        var func = Expression.Lambda<Func<Task<ulong>>>(exp).Compile();

        Assert.Equal(version, func().GetAwaiter().GetResult());
        eventStore.Verify();
    }
    
    private record TestAggregate
    {
        public static Events.Created Create([Command] Commands.Create cmd) => new (cmd.Id, cmd.Value);
        public static Task<Events.Created> CreateAsync([Command] Commands.Create cmd) => 
            Task.FromResult(new Events.Created(cmd.Id, cmd.Value));

        public static readonly MethodInfo CreateMethod = typeof(TestAggregate).GetMethod(nameof(Create))!;
        
        public static readonly MethodInfo CreateAsyncMethod = typeof(TestAggregate).GetMethod(nameof(CreateAsync))!;
    }
}