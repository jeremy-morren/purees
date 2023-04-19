using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using PureES.Core.ExpBuilders.AggregateCmdHandlers;
using PureES.Core.ExpBuilders.WhenHandlers;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace PureES.Core.ExpBuilders;

internal class CommandServicesBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public CommandServicesBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public void AddCommandHandlers(Type aggregateType, IServiceCollection services)
    {
        foreach (var m in aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            if (HandlerHelpers.IsCreateHandler(aggregateType, m))
            {
                var @delegate = CompileCreateOnHandler(aggregateType, m, out var delegateType);
                services.AddSingleton(delegateType, @delegate);
            }
            else if (HandlerHelpers.IsUpdateHandler(aggregateType, m))
            {
                var @delegate = CompileUpdateOnHandler(aggregateType, m, out var delegateType);
                services.AddSingleton(delegateType, @delegate);
            }
    }

    private Delegate CompileCreateOnHandler(Type aggregateType, MethodInfo method, out Type delegateType)
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
        delegateType = typeof(Func<,,,>).MakeGenericType(command.Type, provider.Type, ct.Type, resultType);
        return Expression.Lambda(delegateType,
                exp,
                $"CommandHandler<{cmdType}>",
                true,
                new[] {command, provider, ct})
            .Compile();
    }

    private Delegate CompileUpdateOnHandler(Type aggregateType, MethodInfo method, out Type delegateType)
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
        delegateType = typeof(Func<,,,>).MakeGenericType(command.Type, provider.Type, ct.Type, resultType);
        return Expression.Lambda(delegateType,
                exp,
                $"CommandHandler<{cmdType}>",
                true,
                new[] {command, provider, ct})
            .Compile();
    }

    /// <summary>
    ///     Compiles a <c>AggregateFactory&lt;TAggregate&gt;</c>
    /// </summary>
    public Delegate CompileFactory(Type aggregateType, out Type delegateType)
    {
        var events = Expression.Parameter(typeof(IAsyncEnumerable<EventEnvelope>));
        var services = Expression.Parameter(typeof(IServiceProvider));
        var ct = Expression.Parameter(typeof(CancellationToken));
        var exp = new FactoryExpBuilder(_options)
            .BuildExpression(aggregateType, events, services, ct);

        //Output is AggregateFactory<TAggregate>
        delegateType = typeof(AggregateFactory<>).MakeGenericType(aggregateType);
        var lambda = Expression.Lambda(delegateType, exp, $"Factory<{aggregateType}>", true, new[] {events, services, ct});
        
        return lambda.Compile();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ILogger GetLogger(IServiceProvider services)
    {
        var factory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        return factory.CreateLogger("PureES.CommandHandler");
    }
}