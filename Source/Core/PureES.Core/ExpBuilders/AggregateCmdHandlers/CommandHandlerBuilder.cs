using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders.WhenHandlers;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

public class CommandHandlerBuilder
{
    private readonly CommandHandlerOptions _options;

    public CommandHandlerBuilder(CommandHandlerOptions options) => _options = options;

    public void AddCommandServices(IServiceCollection services, Type aggregateType)
    {
        if (aggregateType.GetCustomAttribute(typeof(AggregateAttribute)) == null)
            throw new ArgumentException("Type is not an aggregate");
        var addHandler = GetType().GetMethod(nameof(AddCommandHandler),
                      BindingFlags.NonPublic | BindingFlags.Static)
                  ?? throw new InvalidOperationException($"Unable to get {nameof(AddCommandHandler)} method");
        foreach (var m in aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            //TODO: Confirm no efficiency loss with MakeGenericMethod
            //TODO: Log handler method matches
            if (HandlerHelpers.IsCreateHandler(aggregateType, m))
            {
                var @delegate = CompileCreateOnHandler(aggregateType, m);
                var cmdType = HandlerHelpers.GetCommandType(m);
                addHandler.MakeGenericMethod(cmdType).Invoke(null, new object?[] {services, @delegate});
            }
            else if (HandlerHelpers.IsUpdateHandler(aggregateType, m))
            {
                var @delegate = CompileUpdateOnHandler(aggregateType, m);
                var cmdType = HandlerHelpers.GetCommandType(m);
                addHandler.MakeGenericMethod(cmdType).Invoke(null, new object?[] {services, @delegate});
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

    private static void AddCommandHandler<T>(IServiceCollection services,
        Lazy<Func<T, IServiceProvider, CancellationToken, Task<ulong>>> handler)
    {
        services.AddTransient<CommandHandler<T>>(sp => (cmd, ct) => handler.Value(cmd, sp, ct));
    }

    private Delegate CompileCreateOnHandler(Type aggregateType, MethodInfo method)
    {
        //Results in Func<TCommand, IServiceProvider, CancellationToken?, Task<ulong>>
        var cmdType = HandlerHelpers.GetCommandType(method);
        var cmd = Expression.Parameter(cmdType);
        var provider = Expression.Parameter(typeof(IServiceProvider));
        var ct = Expression.Parameter(typeof(CancellationToken?));
        var exp = new CreateOnHandlerExpBuilder(_options)
            .BuildCreateOnExpression(aggregateType, method, cmd, provider, ct);
        var type = typeof(Func<,,,>).MakeGenericType(cmd.Type, provider.Type, ct.Type, typeof(Task<ulong>));
        return Expression.Lambda(
                type, exp, $"CommandHandler<{cmdType}>", true, new[] {cmd, provider, ct})
            .Compile();
    }
    
    private Delegate CompileUpdateOnHandler(Type aggregateType, MethodInfo method)
    {
        //Results in Func<TCommand, IServiceProvider, CancellationToken?, Task<ulong>>
        var cmdType = HandlerHelpers.GetCommandType(method);
        var cmd = Expression.Parameter(cmdType);
        var provider = Expression.Parameter(typeof(IServiceProvider));
        var ct = Expression.Parameter(typeof(CancellationToken?));
        var exp = new UpdateOnHandlerExpBuilder(_options)
            .BuildUpdateOnExpression(aggregateType, method, cmd, provider, ct);
        var type = typeof(Func<,,,>).MakeGenericType(cmd.Type, provider.Type, ct.Type, typeof(Task<ulong>));
        return Expression.Lambda(
                type, exp, $"CommandHandler<{cmdType}>", true, new[] {cmd, provider, ct})
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