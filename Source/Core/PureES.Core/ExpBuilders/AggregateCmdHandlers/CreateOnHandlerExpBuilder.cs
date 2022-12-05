using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

// ReSharper disable SuggestBaseTypeForParameter

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal class CreateOnHandlerExpBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public CreateOnHandlerExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression BuildCreateOnExpression(Type aggregateType,
        MethodInfo handlerMethod,
        Expression command,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (!HandlerHelpers.IsCreateHandler(aggregateType, handlerMethod))
            throw new ArgumentException("Invalid create handler method");
        var commandType = HandlerHelpers.GetCommandType(handlerMethod);
        if (command.Type != commandType)
            throw new ArgumentException($"Invalid command {command.Type}", nameof(command));
        MethodInfo method;

        if (handlerMethod.ReturnType.IsValueTask(out var returnType))
        {
            if (returnType == null)
                throw new ArgumentException("Return type cannot be non-generic ValueTask");
            if (HandlerHelpers.IsCommandResult(returnType, out var eventType, out var resultType))
                method = HandleCreateOn.CreateOnValueTaskAsyncResultMethod
                    .MakeGenericMethod(commandType, returnType, eventType, resultType);
            else
                method = HandleCreateOn.CreateOnValueTaskAsyncMethod
                    .MakeGenericMethod(commandType, returnType);
        }
        else if (handlerMethod.ReturnType.IsTask(out returnType))
        {
            if (returnType == null)
                throw new ArgumentException("Return type cannot be non-generic Task");
            if (HandlerHelpers.IsCommandResult(returnType, out var eventType, out var resultType))
                method = HandleCreateOn.CreateOnAsyncResultMethod
                    .MakeGenericMethod(commandType, returnType, eventType, resultType);
            else
                method = HandleCreateOn.CreateOnAsyncMethod
                    .MakeGenericMethod(commandType, returnType);
        }
        else
        {
            if (handlerMethod.ReturnType == typeof(void))
                throw new ArgumentException("Return type cannot be void");
            if (HandlerHelpers.IsCommandResult(handlerMethod.ReturnType, out var eventType, out var resultType))
                method = HandleCreateOn.CreateOnSyncResultMethod
                    .MakeGenericMethod(commandType, handlerMethod.ReturnType, eventType, resultType);
            else
                method = HandleCreateOn.CreateOnSyncMethod
                    .MakeGenericMethod(commandType, handlerMethod.ReturnType);
        }


        var handler = BuildHandler(aggregateType, handlerMethod, command, serviceProvider, cancellationToken);
        var streamId = new GetStreamIdExpBuilder(_options).GetStreamId(command);
        var log = BuildLogCall(serviceProvider, aggregateType, handlerMethod, commandType);
        
        var handle = Expression.Call(method, command, streamId, handler, serviceProvider, cancellationToken);
        return Expression.Block(log, handle);
    }

    private Expression BuildHandler(Type aggregateType,
        MethodInfo handlerMethod,
        Expression command,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        var handler = new CreateExpBuilder(_options)
            .InvokeCreateHandler(aggregateType, handlerMethod, command, serviceProvider, cancellationToken);
        
        //Desired Func<TResponse> where TResponse is return type
        var handlerType = typeof(Func<>).MakeGenericType(handlerMethod.ReturnType);
        return Expression.Lambda(handlerType, handler, $"CreateOn<{command.Type}>", 
            true, Array.Empty<ParameterExpression>());
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
        logger.LogDebug("Command {Command} matched create handler {Handler}",
            commandType, $"{aggregateType}+{handlerMethod.Name}");
    }

    private static readonly MethodInfo LogMethod =
        typeof(CreateOnHandlerExpBuilder).GetMethod(nameof(LogMatchedMethod),
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new Exception($"Unable to get method {nameof(LogMatchedMethod)}");
}