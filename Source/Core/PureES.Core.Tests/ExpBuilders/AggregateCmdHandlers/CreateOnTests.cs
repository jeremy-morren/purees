using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders.AggregateCmdHandlers;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;

public class CreateOnTests
{
    [Fact]
    public void Validate_Create_Command()
    {
        Assert.True(HandlerHelpers.IsCommandHandler(typeof(TestAggregate), TestAggregate.CreateMethod));
        Assert.True(HandlerHelpers.IsCreateHandler(typeof(TestAggregate), TestAggregate.CreateMethod));
        Assert.False(HandlerHelpers.IsUpdateHandler(typeof(TestAggregate), TestAggregate.CreateMethod));
        
        Assert.True(HandlerHelpers.IsCommandHandler(typeof(TestAggregate), TestAggregate.CreateWithSvcMethod));
        Assert.True(HandlerHelpers.IsCreateHandler(typeof(TestAggregate), TestAggregate.CreateWithSvcMethod));
        Assert.False(HandlerHelpers.IsUpdateHandler(typeof(TestAggregate), TestAggregate.CreateWithSvcMethod));
        
        Assert.True(HandlerHelpers.IsCommandHandler(typeof(TestAggregate), TestAggregate.CreateAsyncMethod));
        Assert.True(HandlerHelpers.IsCreateHandler(typeof(TestAggregate), TestAggregate.CreateAsyncMethod));
        Assert.False(HandlerHelpers.IsUpdateHandler(typeof(TestAggregate), TestAggregate.CreateAsyncMethod));
    }

    [Fact]
    public void Invoke_Create_Command()
    {
        using var services = Services.Build();
        var cmd = Rand.NextInt();
        var ct = new CancellationTokenSource().Token;
        
        var builder = new CreateExpBuilder(new CommandHandlerOptions());
        var exp = builder.InvokeCreateHandler(typeof(TestAggregate),
            TestAggregate.CreateMethod,
            Expression.Constant(cmd),
            Expression.Constant(services, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<(string, CancellationToken)>>(exp).Compile();
        
        Assert.Equal((cmd.ToString(), ct), func());
    }
    
    [Fact]
    public void Invoke_CreateAsync_Command()
    {
        using var services = Services.Build();
        var cmd = Rand.NextInt();
        var ct = new CancellationTokenSource().Token;
        
        var builder = new CreateExpBuilder(new CommandHandlerOptions());
        var exp = builder.InvokeCreateHandler(typeof(TestAggregate),
            TestAggregate.CreateAsyncMethod,
            Expression.Constant(cmd),
            Expression.Constant(services, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<string>>>(exp).Compile();

        Assert.Equal(cmd.ToString(), func().GetAwaiter().GetResult());
    }
    
    [Fact]
    public void Invoke_Create_Command_With_Service()
    {
        var svc = new Service();
        using var services = Services.Build(s => s.AddSingleton(svc));
        var cmd = Rand.NextInt();
        var ct = new CancellationTokenSource().Token;
        
        var builder = new CreateExpBuilder(new CommandHandlerOptions());
        var exp = builder.InvokeCreateHandler(typeof(TestAggregate),
            TestAggregate.CreateWithSvcMethod,
            Expression.Constant(cmd),
            Expression.Constant(services, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<string>>(exp).Compile();

        Assert.Equal(cmd.ToString(), func());
        Assert.Equal(cmd, svc.Value);
    }

    private record TestAggregate
    {
        public static (string, CancellationToken) Create([Command] int cmd, CancellationToken ct) => (cmd.ToString(), ct);
        
        public static string CreateSvc([Command] int cmd, [FromServices] Service svc)
        {
            svc.Value = cmd;
            return cmd.ToString();
        }
        
        public static Task<string> CreateAsync([Command] int cmd, CancellationToken _) => Task.FromResult(cmd.ToString());

        public static readonly MethodInfo CreateMethod = typeof(TestAggregate).GetMethod(nameof(Create))!;
        public static readonly MethodInfo CreateWithSvcMethod = typeof(TestAggregate).GetMethod(nameof(CreateSvc))!;
        public static readonly MethodInfo CreateAsyncMethod = typeof(TestAggregate).GetMethod(nameof(CreateAsync))!;
    }

    private class Service
    {
        public int Value { get; set; } = -1;
    }
}