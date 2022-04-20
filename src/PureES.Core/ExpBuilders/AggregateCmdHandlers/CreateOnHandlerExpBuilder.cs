using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

public class CreateOnHandlerExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public CreateOnHandlerExpBuilder(CommandHandlerOptions options)
    {
        _options = options;
    }

    public Expression BuildCreateOnExpression(Type aggregateType,
        MethodInfo handlerMethod,
        Expression command,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (!HandlerHelpers.IsCreateHandler(aggregateType, handlerMethod))
            throw new ArgumentException("Invalid create handler method");
        MethodInfo method;
        if (handlerMethod.ReturnType.IsTask(out var returnType))
        {
            if (returnType == null)
                throw new ArgumentException("Return type cannot be non-generic Task");
            method = GetType().GetMethod(nameof(CreateOnAsync), BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Unable to get {nameof(CreateOnAsync)} method");
            method = method.MakeGenericMethod(HandlerHelpers.GetCommandType(handlerMethod), returnType);
        }
        else
        {
            if (handlerMethod.ReturnType == typeof(void))
                throw new ArgumentException("Return type cannot be void");
            method = GetType().GetMethod(nameof(CreateOnSync), BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Unable to get {nameof(CreateOnSync)} method");
            method = method.MakeGenericMethod(HandlerHelpers.GetCommandType(handlerMethod), handlerMethod.ReturnType);
        }
        if (serviceProvider.Type != typeof(IServiceProvider))
            throw new ArgumentException("Invalid ServiceProvider expression");
        if (cancellationToken.Type != typeof(CancellationToken?))
            throw new ArgumentException("Invalid CancellationToken? expression");
        var getStreamId = new CommandHandlerBuilder(_options)
            .GetStreamId(HandlerHelpers.GetCommandType(handlerMethod));
        var handler = BuildCreateHandler(aggregateType, handlerMethod);
        var getMetadata = Expression.Constant(_options.GetMetadata, typeof(GetMetadata));
        return Expression.Call(method, command, getStreamId, handler, getMetadata, serviceProvider, cancellationToken);
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
    
    #region Implementations

    private static async Task<ulong> CreateOnSync<TCommand, TEvent>(TCommand command,
        Func<TCommand, string> getStreamId,
        Func<TCommand, IServiceProvider, CancellationToken, TEvent> handle,
        GetMetadata? getMetadata,
        IServiceProvider serviceProvider,
        CancellationToken? cancellationToken = default)
    where TCommand : notnull
    where TEvent : notnull
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(CommandHandlerBuilder.LoggerCategory);
        try
        {
            logger.LogInformation("Handling command {@Command}", typeof(TCommand));
            var ct = cancellationToken ?? CommandHandlerBuilder.HttpContext?.RequestAborted ?? default;
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var @event = (object)handle(command, serviceProvider, ct);
            var metadata = getMetadata?.Invoke(command, @event);
            var version = await store.Create(getStreamId(command),
                new UncommittedEvent(Guid.NewGuid(), @event, metadata), ct);
            logger.LogInformation("Handled command {@Command}", typeof(TCommand));
            return version;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
    
    private static async Task<ulong> CreateOnAsync<TCommand, TEvent>(TCommand command,
        Func<TCommand, string> getStreamId,
        Func<TCommand, IServiceProvider, CancellationToken, Task<TEvent>> handle,
        GetMetadata? getMetadata,
        IServiceProvider serviceProvider,
        CancellationToken? cancellationToken = default)
    where TCommand : notnull
    where TEvent : notnull
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(CommandHandlerBuilder.LoggerCategory);
        try
        {
            logger.LogInformation("Handling command {@Command}", typeof(TCommand));
            var ct = cancellationToken ?? CommandHandlerBuilder.HttpContext?.RequestAborted ?? default;
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var @event = (object)await handle(command, serviceProvider, ct);
            var metadata = getMetadata?.Invoke(command, @event);
            var version = await store.Create(getStreamId(command),
                new UncommittedEvent(Guid.NewGuid(), @event, metadata), ct);
            logger.LogInformation("Handled command {@Command}", typeof(TCommand));
            return version;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
    
    
    #endregion
    
}