using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PureES.Core.ExpBuilders.WhenHandlers;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

public class UpdateOnHandlerExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public UpdateOnHandlerExpBuilder(CommandHandlerOptions options)
    {
        _options = options;
    }
    
    public Expression BuildUpdateOnExpression(Type aggregateType,
        MethodInfo handlerMethod,
        Expression command,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (!HandlerHelpers.IsUpdateHandler(aggregateType, handlerMethod))
            throw new ArgumentException("Invalid update handler method");
        MethodInfo method;
        if (handlerMethod.ReturnType.IsTask(out var returnType))
        {
            if (returnType == null)
                throw new ArgumentException("Return type cannot be non-generic Task");
            method = GetType().GetMethod(nameof(UpdateOnAsync), BindingFlags.NonPublic | BindingFlags.Static)
                         ?? throw new InvalidOperationException($"Unable to get {nameof(UpdateOnAsync)} method");
            method = method.MakeGenericMethod(aggregateType, HandlerHelpers.GetCommandType(handlerMethod), returnType);
        }
        else
        {
            if (handlerMethod.ReturnType == typeof(void))
                throw new ArgumentException("Return type cannot be void");
            method = GetType().GetMethod(nameof(UpdateOnSync), BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Unable to get {nameof(UpdateOnSync)} method");
            method = method.MakeGenericMethod(aggregateType, HandlerHelpers.GetCommandType(handlerMethod), handlerMethod.ReturnType);
        }
        if (serviceProvider.Type != typeof(IServiceProvider))
            throw new ArgumentException("Invalid ServiceProvider expression");
        if (cancellationToken.Type != typeof(CancellationToken?))
            throw new ArgumentException("Invalid CancellationToken? expression");
        var getStreamId = new CommandHandlerBuilder(_options)
            .GetStreamId(HandlerHelpers.GetCommandType(handlerMethod));
        var getExpectedVersion = Expression.Constant(_options.GetExpectedVersion, typeof(GetExpectedVersion));
        var load = new CommandHandlerBuilder(_options).Load(aggregateType);
        var handler = BuildUpdateHandler(aggregateType, handlerMethod);
        var getMetadata = Expression.Constant(_options.GetMetadata, typeof(GetMetadata));
        return Expression.Call(method,
            command,
            getStreamId,
            getExpectedVersion,
            load,
            handler,
            getMetadata,
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
    
    
    #region Implementation

    private static async Task<ulong> UpdateOnSync<TAggregate, TCommand, TEvent>(TCommand command,
        Func<TCommand, string> getStreamId,
        GetExpectedVersion? getExpectedVersion,
        Func<ImmutableArray<EventEnvelope>, TAggregate> load,
        Func<TAggregate, TCommand, IServiceProvider, CancellationToken, TEvent> handle,
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
            var events = await store.Load(getStreamId(command),
                getExpectedVersion?.Invoke(command, serviceProvider),
                ct);
            var @event = (object)handle(load(events), command, serviceProvider, ct);
            var metadata = getMetadata?.Invoke(command, @event);
            var version = await store.Append(getStreamId(command),
                (ulong)events.Length, new UncommittedEvent(Guid.NewGuid(), @event, metadata), ct);
            logger.LogInformation("Handled command {@Command}", typeof(TCommand));
            return version;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
    
    
    private static async Task<ulong> UpdateOnAsync<TAggregate, TCommand, TEvent>(TCommand command,
        Func<TCommand, string> getStreamId,
        GetExpectedVersion? getExpectedVersion,
        Func<ImmutableArray<EventEnvelope>, TAggregate> load,
        Func<TAggregate, TCommand, IServiceProvider, CancellationToken, Task<TEvent>> handle,
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
            var events = await store.Load(getStreamId(command),
                getExpectedVersion?.Invoke(command, serviceProvider),
                ct);
            var @event = (object)await handle(load(events), command, serviceProvider, ct);
            var metadata = getMetadata?.Invoke(command, @event);
            var version = await store.Append(getStreamId(command),
                (ulong)events.Length, new UncommittedEvent(Guid.NewGuid(), @event, metadata), ct);
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