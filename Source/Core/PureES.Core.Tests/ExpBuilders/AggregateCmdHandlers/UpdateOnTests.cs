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

public class UpdateOnTests
{
    [Fact]
    public void Validate_Update_Command()
    {
        Assert.True(HandlerHelpers.IsCommandHandler(typeof(TestAggregate), TestAggregate.UpdateMethod));
        Assert.False(HandlerHelpers.IsCreateHandler(typeof(TestAggregate), TestAggregate.UpdateMethod));
        Assert.True(HandlerHelpers.IsUpdateHandler(typeof(TestAggregate), TestAggregate.UpdateMethod));
        
        Assert.True(HandlerHelpers.IsCommandHandler(typeof(TestAggregate), TestAggregate.UpdateWithSvcMethod));
        Assert.False(HandlerHelpers.IsCreateHandler(typeof(TestAggregate), TestAggregate.UpdateWithSvcMethod));
        Assert.True(HandlerHelpers.IsUpdateHandler(typeof(TestAggregate), TestAggregate.UpdateWithSvcMethod));
        
        Assert.True(HandlerHelpers.IsCommandHandler(typeof(TestAggregate), TestAggregate.UpdateAsyncMethod));
        Assert.False(HandlerHelpers.IsCreateHandler(typeof(TestAggregate), TestAggregate.UpdateAsyncMethod));
        Assert.True(HandlerHelpers.IsUpdateHandler(typeof(TestAggregate), TestAggregate.UpdateAsyncMethod));
    }
    
    [Fact]
    public void Invoke_Update_Command()
    {
        using var services = Services.Build();
        var agg = new TestAggregate();
        var cmd = Rand.NextInt();
        var ct = new CancellationTokenSource().Token;
        
        var builder = new UpdateExpBuilder(new CommandHandlerOptions());
        var exp = builder.InvokeUpdateHandler(typeof(TestAggregate),
            TestAggregate.UpdateMethod,
            Expression.Constant(agg),
            Expression.Constant(cmd),
            Expression.Constant(services, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<(TestAggregate, string, CancellationToken)>>(exp).Compile();
        
        Assert.Equal((agg, cmd.ToString(), ct), func());
    }
    
    [Fact]
    public void Invoke_UpdateWithSvc_Command()
    {
        var svc = new Service();
        using var services = Services.Build(s => s.AddSingleton(svc));
        var agg = new TestAggregate();
        var cmd = Rand.NextInt();
        var ct = new CancellationTokenSource().Token;
        
        var builder = new UpdateExpBuilder(new CommandHandlerOptions());
        var exp = builder.InvokeUpdateHandler(typeof(TestAggregate),
            TestAggregate.UpdateWithSvcMethod,
            Expression.Constant(agg),
            Expression.Constant(cmd),
            Expression.Constant(services, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<Service>>(exp).Compile();
        
        Assert.Equal(svc, func());
    }
    
    [Fact]
    public void Invoke_UpdateAsync_Command()
    {
        using var services = Services.Build();
        var agg = new TestAggregate();
        var cmd = Rand.NextInt();
        var ct = new CancellationTokenSource().Token;
        
        var builder = new UpdateExpBuilder(new CommandHandlerOptions());
        var exp = builder.InvokeUpdateHandler(typeof(TestAggregate),
            TestAggregate.UpdateAsyncMethod,
            Expression.Constant(agg),
            Expression.Constant(cmd),
            Expression.Constant(services, typeof(IServiceProvider)),
            Expression.Constant(ct));
        var func = Expression.Lambda<Func<Task<string>>>(exp).Compile();
        
        Assert.Equal(cmd.ToString(), func().GetAwaiter().GetResult());
    }

    private record TestAggregate
    {
        public static (TestAggregate, string, CancellationToken) Update(TestAggregate current, [Command] int cmd, CancellationToken ct) => (current, cmd.ToString(), ct);
        
        public static Service UpdateSvc(TestAggregate current, [Command] int cmd, [FromServices] Service svc)
        {
            svc.Value = cmd;
            return svc;
        }
        
        public static Task<string> UpdateAsync(TestAggregate current, [Command] int cmd, CancellationToken _) => Task.FromResult(cmd.ToString());
        
        public static readonly MethodInfo UpdateMethod = typeof(TestAggregate).GetMethod(nameof(Update))!;
        public static readonly MethodInfo UpdateWithSvcMethod = typeof(TestAggregate).GetMethod(nameof(UpdateSvc))!;
        public static readonly MethodInfo UpdateAsyncMethod = typeof(TestAggregate).GetMethod(nameof(UpdateAsync))!;
    }

    private class Service
    {
        public int Value { get; set; } = -1;
    }
}