using System.Linq.Expressions;
using System.Reflection;
using PureES.Core.ExpBuilders.Services;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal class UpdateOnHandlerExpBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public UpdateOnHandlerExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression BuildUpdateOnExpression(Type aggregateType,
        MethodInfo handlerMethod,
        Expression command,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (!HandlerHelpers.IsUpdateHandler(aggregateType, handlerMethod))
            throw new ArgumentException("Invalid update handler method");
        var commandType = HandlerHelpers.GetCommandType(handlerMethod);
        MethodInfo method;
        if (handlerMethod.ReturnType.IsValueTask(out var returnType))
        {
            if (returnType == null)
                throw new ArgumentException("Return type cannot be non-generic Task");
            if (HandlerHelpers.IsCommandResult(returnType, out var eventType, out var resultType))
                method = HandleUpdateOn.UpdateOnValueTaskAsyncWithResultMethod
                    .MakeGenericMethod(aggregateType, commandType, returnType, eventType, resultType);
            else
                method = HandleUpdateOn.UpdateOnValueTaskAsyncMethod
                    .MakeGenericMethod(aggregateType, commandType, returnType);
        }
        else if (handlerMethod.ReturnType.IsTask(out returnType))
        {
            if (returnType == null)
                throw new ArgumentException("Return type cannot be non-generic Task");
            if (HandlerHelpers.IsCommandResult(returnType, out var eventType, out var resultType))
                method = HandleUpdateOn.UpdateOnAsyncWithResultMethod
                    .MakeGenericMethod(aggregateType, commandType, returnType, eventType, resultType);
            else
                method = HandleUpdateOn.UpdateOnAsyncMethod
                    .MakeGenericMethod(aggregateType, commandType, returnType);
        }
        else
        {
            if (handlerMethod.ReturnType == typeof(void))
                throw new ArgumentException("Return type cannot be void");
            if (HandlerHelpers.IsCommandResult(handlerMethod.ReturnType, out var eventType, out var resultType))
                method = HandleUpdateOn.UpdateOnSyncWithResultMethod
                    .MakeGenericMethod(aggregateType, commandType, handlerMethod.ReturnType, eventType, resultType);
            else
                method = HandleUpdateOn.UpdateOnSyncMethod
                    .MakeGenericMethod(aggregateType, commandType, handlerMethod.ReturnType);
        }

        if (serviceProvider.Type != typeof(IServiceProvider))
            throw new ArgumentException("Invalid ServiceProvider expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new ArgumentException("Invalid CancellationToken expression");
        var getStreamId = new CommandServicesBuilder(_options)
            .GetStreamId(HandlerHelpers.GetCommandType(handlerMethod));
        var load = new CommandServicesBuilder(_options).Factory(aggregateType);
        var handler = BuildUpdateHandler(aggregateType, handlerMethod);
        return Expression.Call(method,
            command,
            getStreamId,
            load,
            handler,
            serviceProvider,
            cancellationToken);
    }

    private ConstantExpression BuildUpdateHandler(Type aggregateType, MethodInfo handlerMethod)
    {
        var cmdType = HandlerHelpers.GetCommandType(handlerMethod);
        var current = Expression.Parameter(aggregateType);
        var cmd = Expression.Parameter(cmdType);
        var sp = Expression.Parameter(typeof(IServiceProvider));
        var ct = Expression.Parameter(typeof(CancellationToken));
        var exp = new UpdateExpBuilder(_options)
            .InvokeUpdateHandler(aggregateType, handlerMethod, current, cmd, sp, ct);
        //Desired Func<TAggregate, TCommand, IServiceProvider, CancellationToken, TEvent>
        //where TEvent is return type
        var type = typeof(Func<,,,,>).MakeGenericType(
            aggregateType,
            cmdType,
            typeof(IServiceProvider),
            typeof(CancellationToken),
            handlerMethod.ReturnType);
        var lambda = Expression.Lambda(type, exp, "UpdateOn", true, new[] {current, cmd, sp, ct});
        return Expression.Constant(lambda.Compile(), type);
    }

    //TODO: Log matched method + aggregate
}