using System.Collections;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PureES.Core.EventStore;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal static class HandleCreateOn
{
    public static async Task<ulong> CreateOnSync<TCommand, TResponse>(TCommand command,
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
    
    public static async Task<TResult> CreateOnSyncWithResult<TCommand, TResponse, TEvent, TResult>(TCommand command,
        Func<TCommand, string> getStreamId,
        Func<TCommand, IServiceProvider, CancellationToken, TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct) 
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(CommandHandlerBuilder.LoggerCategory);
        try
        {
            logger.LogInformation("Handling command {@Command}", typeof(TCommand));
            var response = handle(command, serviceProvider, ct);
            await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response.Event, ct);
            return response.Result;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
    
    public static async Task<ulong> CreateOnAsync<TCommand, TResponse>(TCommand command,
        Func<TCommand, string> getStreamId,
        Func<TCommand, IServiceProvider, CancellationToken, Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct) 
        where TCommand : notnull
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
    
    public static async Task<TResult> CreateOnAsyncWithResult<TCommand, TResponse, TEvent, TResult>(TCommand command,
        Func<TCommand, string> getStreamId,
        Func<TCommand, IServiceProvider, CancellationToken, Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct) 
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(CommandHandlerBuilder.LoggerCategory);
        try
        {
            logger.LogInformation("Handling command {@Command}", typeof(TCommand));
            var response = await handle(command, serviceProvider, ct);
            await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response.Event, ct);
            return response.Result;
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

    private static MethodInfo GetMethod(string name) 
        => typeof(HandleCreateOn)
               .GetMethods(BindingFlags.Public | BindingFlags.Static)
               .SingleOrDefault(m => m.Name == name) 
           ?? throw new InvalidOperationException($"Unable to get method {name}");

    public static readonly MethodInfo CreateOnSyncMethod = GetMethod(nameof(CreateOnSync));
    public static readonly MethodInfo CreateOnAsyncMethod = GetMethod(nameof(CreateOnAsync));
    public static readonly MethodInfo CreateOnSyncWithResultMethod = GetMethod(nameof(CreateOnSyncWithResult));
    public static readonly MethodInfo CreateOnAsyncWithResultMethod = GetMethod(nameof(CreateOnAsyncWithResult));
}