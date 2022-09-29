using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders.WhenHandlers;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal class CommandHandlerBuilder
{
    private readonly CommandHandlerOptions _options;

    public CommandHandlerBuilder(CommandHandlerOptions options) => _options = options;

    public void AddCommandServices(IServiceCollection services, Type aggregateType)
    {
        if (aggregateType.GetCustomAttribute(typeof(AggregateAttribute)) == null)
            throw new ArgumentException("Type is not an aggregate");
        var add = typeof(CommandHandlerBuilder)
                             .GetMethod(nameof(AddHandler), BindingFlags.Static | BindingFlags.NonPublic)
                         ?? throw new Exception("Unable to get AddHandler method");
        var addWithResult = typeof(CommandHandlerBuilder)
                                .GetMethod(nameof(AddHandlerWithResult), BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Exception("Unable to get AddHandlerWithResult method");
        foreach (var m in aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            //TODO: Confirm no efficiency loss with MakeGenericMethod
            if (HandlerHelpers.IsCreateHandler(aggregateType, m))
            {
                var @delegate = CompileCreateOnHandler(aggregateType, m);
                var cmdType = HandlerHelpers.GetCommandType(m);
                if (HandlerHelpers.ReturnsCommandResult(m, out var resultType))
                    addWithResult.MakeGenericMethod(cmdType, resultType)
                        .Invoke(null, new object?[] {services, @delegate});
                else 
                    add.MakeGenericMethod(cmdType).Invoke(null, new object?[] {services, @delegate});
            }
            else if (HandlerHelpers.IsUpdateHandler(aggregateType, m))
            {
                var @delegate = CompileUpdateOnHandler(aggregateType, m);
                var cmdType = HandlerHelpers.GetCommandType(m);
                if (HandlerHelpers.ReturnsCommandResult(m, out var resultType))
                    addWithResult.MakeGenericMethod(cmdType, resultType)
                        .Invoke(null, new object?[] {services, @delegate});
                else 
                    add.MakeGenericMethod(cmdType).Invoke(null, new object?[] {services, @delegate});
            }
        }
        //Add load method
        var load = Load(aggregateType);
        services.Add(new ServiceDescriptor(load.Type, load.Value));
        
        //Add aggregate store
        var storeType = typeof(IAggregateStore<>).MakeGenericType(aggregateType);
        var implType = typeof(AggregateStore<>).MakeGenericType(aggregateType);
        services.Add(new ServiceDescriptor(storeType,implType, ServiceLifetime.Transient));
    }

    private static void AddHandler<TCommand>(IServiceCollection services,
        Func<TCommand, IServiceProvider, CancellationToken, Task<ulong>> handler)
    {
        services.AddTransient<CommandHandler<TCommand>>(sp => (cmd, ct) => handler(cmd, sp, ct));
    }
    
    private static void AddHandlerWithResult<TCommand, TResult>(IServiceCollection services,
        Func<TCommand, IServiceProvider, CancellationToken, Task<TResult>> handler)
    {
        services.AddTransient<CommandHandler<TCommand, TResult>>(sp => (cmd, ct) => handler(cmd, sp, ct));
    }

    private Delegate CompileCreateOnHandler(Type aggregateType, MethodInfo method)
    {
        if (!HandlerHelpers.ReturnsCommandResult(method, out var resultType))
            resultType = typeof(ulong);
        resultType = typeof(Task<>).MakeGenericType(resultType);
        
        //Results in Func<TCommand, IServiceProvider, CancellationToken?, Task<resultType>>
        var cmdType = HandlerHelpers.GetCommandType(method);
        var command = Expression.Parameter(cmdType);
        var provider = Expression.Parameter(typeof(IServiceProvider));
        var ct = Expression.Parameter(typeof(CancellationToken));
        var exp = new CreateOnHandlerExpBuilder(_options)
            .BuildCreateOnExpression(aggregateType, method, command, provider, ct);
        var type = typeof(Func<,,,>).MakeGenericType(command.Type, provider.Type, ct.Type, resultType);
        return Expression.Lambda(
                type, exp, $"CommandHandler<{cmdType}>", true, new[] {command, provider, ct})
            .Compile();
    }
    
    private Delegate CompileUpdateOnHandler(Type aggregateType, MethodInfo method)
    {
        if (!HandlerHelpers.ReturnsCommandResult(method, out var resultType))
            resultType = typeof(ulong);
        resultType = typeof(Task<>).MakeGenericType(resultType);
        
        //Results in Func<TCommand, IServiceProvider, CancellationToken?, Task<ulong>>
        var cmdType = HandlerHelpers.GetCommandType(method);
        var command = Expression.Parameter(cmdType);
        var provider = Expression.Parameter(typeof(IServiceProvider));
        var ct = Expression.Parameter(typeof(CancellationToken));
        var exp = new UpdateOnHandlerExpBuilder(_options)
            .BuildUpdateOnExpression(aggregateType, method, command, provider, ct);
        var type = typeof(Func<,,,>).MakeGenericType(command.Type, provider.Type, ct.Type, resultType);
        return Expression.Lambda(
                type, exp, $"CommandHandler<{cmdType}>", true, new[] {command, provider, ct})
            .Compile();
    }

    /// <summary>
    /// Compiles a <c>Func&lt;TCommand, string&gt;</c> delegate
    /// </summary>
    public ConstantExpression GetStreamId(Type commandType)
    {
        var param = Expression.Parameter(commandType);
        var exp = new GetStreamIdExpBuilder(_options).GetStreamId(param);
        var type = typeof(Func<,>).MakeGenericType(commandType, typeof(string));
        var lambda = Expression.Lambda(type, exp, "GetStreamId", true, new[] {param});
        return Expression.Constant(lambda.Compile(), type);
    }
    
    /// <summary>
    /// Compiles a <c>AggregateFactory&lt;TAggregate&gt;</c>
    /// </summary>
    public ConstantExpression Load(Type aggregateType)
    {
        var events = Expression.Parameter(typeof(IAsyncEnumerable<EventEnvelope>));
        var ct = Expression.Parameter(typeof(CancellationToken));
        var exp = new FactoryExpBuilder(_options)
            .BuildExpression(aggregateType, events, ct);
        
        //Output is AggregateFactory<TAggregate>
        var type = typeof(AggregateFactory<>).MakeGenericType(aggregateType);
        var lambda = Expression.Lambda(type, exp, "Load", true, new[] {events, ct});
        return Expression.Constant(lambda.Compile(), type);
    }

    public const string LoggerCategory = "PureES.CommandHandler";
}