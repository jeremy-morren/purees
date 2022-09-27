﻿using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

public class CreateOnHandlerExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public CreateOnHandlerExpBuilder(CommandHandlerOptions options) => _options = options;

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
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new ArgumentException("Invalid CancellationToken expression");
        var getStreamId = new CommandHandlerBuilder(_options)
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
    
    #region Implementations

    private static async Task<ulong> CreateOnSync<TCommand, TResponse>(TCommand command,
        Func<TCommand, string> getStreamId,
        Func<TCommand, IServiceProvider, CancellationToken, TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct) 
        where TCommand : notnull
        where TResponse : notnull
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(CommandHandlerBuilder.LoggerCategory);
        try
        {
            logger.LogInformation("Handling command {@Command}", typeof(TCommand));
            var response = handle(command, serviceProvider, ct);
            return await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
    
    private static async Task<ulong> CreateOnAsync<TCommand, TResponse>(TCommand command,
        Func<TCommand, string> getStreamId,
        Func<TCommand, IServiceProvider, CancellationToken, Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct) 
        where TCommand : notnull
        where TResponse : notnull
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(CommandHandlerBuilder.LoggerCategory);
        try
        {
            logger.LogInformation("Handling command {@Command}", typeof(TCommand));
            var response = await handle(command, serviceProvider, ct);
            return await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
    
    
    /// <summary>
    /// Persists a create command handler response to <see cref="IEventStore"/>
    /// </summary>
    /// <returns></returns>
    private static async Task<ulong> ProcessCreateResponse<TCommand, TResponse>(ILogger logger,
        IServiceProvider serviceProvider,
        Func<TCommand, string> getStreamId,
        TCommand command,
        TResponse? response, 
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : notnull
    {
        if (response == null)
            throw new InvalidOperationException($"Command {typeof(TCommand)} returned no response");
        var store = serviceProvider.GetRequiredService<IEventStore>();
        var enricher = serviceProvider.GetRequiredService<IEventEnricher>();
        object? metadata;
        ulong revision;
        var streamId = getStreamId(command);
        if (response is IEnumerable enumerable)
        {
            var events = new List<UncommittedEvent>();
            foreach (var e in enumerable)
            {
                metadata = await enricher.GetMetadata(command, e, ct);
                events.Add(new UncommittedEvent(Guid.NewGuid(), e, metadata));
            }
            revision = await store.Create(streamId, events, ct);
        }
        else
        {
            metadata = await enricher.GetMetadata(command, response, ct);
            var @event = new UncommittedEvent(Guid.NewGuid(), response, metadata);
            revision = await store.Create(streamId, @event, ct);
        }
        logger.LogInformation("Successfully handled {@Command}. Stream {StreamId} now at revision {Revision}",
            typeof(TCommand), streamId, revision);
        return revision;
    }
    
    
    #endregion
    
}