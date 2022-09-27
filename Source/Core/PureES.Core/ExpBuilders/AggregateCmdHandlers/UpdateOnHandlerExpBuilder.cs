using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PureES.Core.EventStore;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal class UpdateOnHandlerExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public UpdateOnHandlerExpBuilder(CommandHandlerOptions options) => _options = options;

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
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new ArgumentException("Invalid CancellationToken expression");
        var getStreamId = new CommandHandlerBuilder(_options)
            .GetStreamId(HandlerHelpers.GetCommandType(handlerMethod));
        var load = new CommandHandlerBuilder(_options).Load(aggregateType);
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
    
    #region Implementation

    private static async Task<ulong> UpdateOnSync<TAggregate, TCommand, TResponse>(TCommand command,
        Func<TCommand, string> getStreamId,
        AggregateFactory<TAggregate> factory,
        Func<TAggregate, TCommand, IServiceProvider, CancellationToken, TResponse> handle,
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
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var expectedVersion = await serviceProvider.GetExpectedVersion(command, ct);
            var events = expectedVersion != null
                ? store.Load(getStreamId(command), expectedVersion.Value, ct)
                : store.Load(getStreamId(command), ct);
            var current = await factory(@events, ct);
            var response = handle(current.Aggregate, command, serviceProvider, ct);
            return await ProcessUpdateResponse(logger, serviceProvider, getStreamId, current.Revision, command, response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
    
    private static async Task<ulong> UpdateOnAsync<TAggregate, TCommand, TResponse>(TCommand command,
        Func<TCommand, string> getStreamId,
        AggregateFactory<TAggregate> factory,
        Func<TAggregate, TCommand, IServiceProvider, CancellationToken, Task<TResponse>> handle,
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
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var expectedVersion = await serviceProvider.GetExpectedVersion(@command, ct);
            var events = expectedVersion != null
                ? store.Load(getStreamId(command), expectedVersion.Value, ct)
                : store.Load(getStreamId(command), ct);
            var current = await factory(@events, ct);
            var response = await handle(current.Aggregate, command, serviceProvider, ct);
            return await ProcessUpdateResponse(logger, 
                serviceProvider,
                getStreamId, 
                current.Revision, 
                command, 
                response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }

    /// <summary>
    /// Persists an update command handler response to <see cref="IEventStore"/>
    /// </summary>
    /// <returns></returns>
    private static async Task<ulong> ProcessUpdateResponse<TCommand, TResponse>(ILogger logger,
        IServiceProvider serviceProvider,
        Func<TCommand, string> getStreamId,
        ulong currentRevision,
        TCommand command,
        TResponse? response, 
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : notnull
    {
        if (response == null)
        {
            logger.LogInformation("Command {@Command} returned no result", typeof(TCommand));
            return currentRevision;
        }
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
            revision = await store.Append(streamId, currentRevision, events, ct);
        }
        else
        {
            metadata = await enricher.GetMetadata(command, response, ct);
            var @event = new UncommittedEvent(Guid.NewGuid(), response, metadata);
            revision = await store.Append(streamId, currentRevision, @event, ct);
        }
        logger.LogInformation("Successfully handled {@Command}. Stream {StreamId} now at revision {Revision}",
            typeof(TCommand), streamId, revision);
        return revision;
    }
    
    #endregion
}