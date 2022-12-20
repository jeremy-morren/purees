

// ReSharper disable SuggestBaseTypeForParameter

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal class UpdateOnHandlerExpBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public UpdateOnHandlerExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    //TODO: Log matched method + aggregate
    
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

        var streamId = new GetStreamIdExpBuilder(_options).GetStreamId(command);
        
        var log = BuildLogCall(serviceProvider, aggregateType, handlerMethod, commandType);

        var handler = BuildUpdateHandler(aggregateType, handlerMethod, command, serviceProvider, cancellationToken);

        var invoke = Expression.Call(method,
            command,
            streamId,
            handler,
            serviceProvider,
            cancellationToken);

        return Expression.Block(log, invoke);
    }

    private Expression BuildUpdateHandler(Type aggregateType,
        MethodInfo handlerMethod, 
        Expression command,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        var current = Expression.Parameter(aggregateType);
        
        var exp = new UpdateExpBuilder(_options)
            .InvokeUpdateHandler(aggregateType, handlerMethod, current, command, serviceProvider, cancellationToken);
        //Desired Func<TAggregate, TResponse>
        //where TResponse is return type
        var type = typeof(Func<,>).MakeGenericType(aggregateType, handlerMethod.ReturnType);
        return Expression.Lambda(type, exp, $"UpdateOn<{command.Type}>", true, new[] {current });
    }
    
    
    private static Expression BuildLogCall(Expression serviceProvider,
        Type aggregateType,
        MethodInfo handlerMethod,
        Type commandType) =>
        Expression.Call(LogMethod, 
            serviceProvider,
            Expression.Constant(aggregateType),
            Expression.Constant(handlerMethod),
            Expression.Constant(commandType));

    private static void LogMatchedMethod(IServiceProvider serviceProvider, 
        Type aggregateType,
        MethodInfo handlerMethod,
        Type commandType)
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        logger.LogDebug("Command {Command} matched update handler {Handler}",
            commandType, $"{aggregateType}+{handlerMethod.Name}");
    }

    private static readonly MethodInfo LogMethod =
        typeof(CreateOnHandlerExpBuilder).GetMethod(nameof(LogMatchedMethod),
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new Exception($"Unable to get method {nameof(LogMatchedMethod)}");
}