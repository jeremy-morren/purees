using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PureES.Core.EventStore;
using PureES.Core.ExpBuilders.Services;

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
        MethodInfo method;
        if (handlerMethod.ReturnType.IsTask(out var returnType))
        {
            if (returnType == null)
                throw new ArgumentException("Return type cannot be non-generic Task");
            if (HandlerHelpers.IsCommandResult(returnType, out var eventType, out var resultType))
                method = HandleCreateOn.CreateOnAsyncWithResultMethod
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
                method = HandleCreateOn.CreateOnSyncWithResultMethod
                    .MakeGenericMethod(commandType, handlerMethod.ReturnType, eventType, resultType);
            else
                method = HandleCreateOn.CreateOnSyncMethod
                    .MakeGenericMethod(commandType, handlerMethod.ReturnType);
        }
        if (serviceProvider.Type != typeof(IServiceProvider))
            throw new ArgumentException("Invalid ServiceProvider expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new ArgumentException("Invalid CancellationToken expression");
        var getStreamId = new CommandServicesBuilder(_options)
            .GetStreamId(HandlerHelpers.GetCommandType(handlerMethod));
        var handler = BuildCreateHandler(aggregateType, handlerMethod);
        return Expression.Call(method, command, getStreamId, handler, serviceProvider, cancellationToken);
    }

    private ConstantExpression BuildCreateHandler(Type aggregateType, MethodInfo handlerMethod)
    {
        var cmdType = HandlerHelpers.GetCommandType(handlerMethod);
        var cmd = Expression.Parameter(cmdType);
        var sp = Expression.Parameter(typeof(IServiceProvider));
        var ct = Expression.Parameter(typeof(CancellationToken));
        var exp = new CreateExpBuilder(_options)
            .InvokeCreateHandler(aggregateType, handlerMethod, cmd, sp, ct);
        //Desired Func<TCommand, IServiceProvider, TEvent>
        //where TEvent is return type
        var type = typeof(Func<,,,>).MakeGenericType(cmdType, 
            typeof(IServiceProvider), 
            typeof(CancellationToken), 
            handlerMethod.ReturnType);
        var lambda = Expression.Lambda(type, exp, "CreateOn", true, new[] {cmd, sp, ct});
        return Expression.Constant(lambda.Compile(), type);
    }
    
}