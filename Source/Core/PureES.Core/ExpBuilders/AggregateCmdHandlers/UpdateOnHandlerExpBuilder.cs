using System.Linq.Expressions;
using System.Reflection;
using PureES.Core.ExpBuilders.Services;
using PureES.Core.ExpBuilders.WhenHandlers;

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

        var getStreamId = BuildGetStreamId(command);
        
        var factory = BuildFactory(aggregateType, serviceProvider, cancellationToken);

        var handler = BuildUpdateHandler(aggregateType, handlerMethod, command, serviceProvider, cancellationToken);
        
        return Expression.Call(method,
            command,
            getStreamId,
            factory,
            handler,
            serviceProvider,
            cancellationToken);
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

    private Expression BuildFactory(Type aggregateType, Expression serviceProvider, Expression cancellationToken)
    {
        var events = Expression.Parameter(typeof(IAsyncEnumerable<EventEnvelope>));
        var exp = new FactoryExpBuilder(_options)
            .BuildExpression(aggregateType, events, serviceProvider, cancellationToken);

        aggregateType = typeof(LoadedAggregate<>).MakeGenericType(aggregateType);

        //Output is Func<IAsyncEnumerable<EventEnvelope>, ValueTask<TAggregate>>
        var type = typeof(Func<,>).MakeGenericType(typeof(IAsyncEnumerable<EventEnvelope>),
            typeof(ValueTask<>).MakeGenericType(aggregateType));
        return Expression.Lambda(type, exp, $"Factory<{aggregateType}>", true, new[] {events});
    }
    
    private Expression BuildGetStreamId(Expression command)
    {
        var streamId = new GetStreamIdExpBuilder(_options).GetStreamId(command);
        
        //Desired return type is Func<string>()
        return Expression.Lambda<Func<string>>(streamId, 
            $"GetStreamId<{command.Type}>", 
            true,
            ArraySegment<ParameterExpression>.Empty);
    }
}