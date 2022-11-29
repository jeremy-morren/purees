using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PureES.Core.EventStore;
using PureES.Core.ExpBuilders.Services;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal static class HandleCreateOn
{
    public static readonly MethodInfo CreateOnSyncMethod = typeof(HandleCreateOn).GetStaticMethod(nameof(CreateOnSync));

    public static readonly MethodInfo CreateOnAsyncMethod =
        typeof(HandleCreateOn).GetStaticMethod(nameof(CreateOnAsync));

    public static readonly MethodInfo CreateOnValueTaskAsyncMethod =
        typeof(HandleCreateOn).GetStaticMethod(nameof(CreateOnValueTaskAsync));

    public static readonly MethodInfo CreateOnSyncResultMethod =
        typeof(HandleCreateOn).GetStaticMethod(nameof(CreateOnSyncResult));

    public static readonly MethodInfo CreateOnAsyncResultMethod =
        typeof(HandleCreateOn).GetStaticMethod(nameof(CreateOnAsyncResult));

    public static readonly MethodInfo CreateOnValueTaskAsyncResultMethod =
        typeof(HandleCreateOn).GetStaticMethod(nameof(CreateOnValueTaskAsyncResult));

    public static async Task<ulong> CreateOnSync<TCommand, TResponse>(TCommand command,
        Func<string> getStreamId,
        Func<TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : notnull
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling create command {@Command}", typeof(TCommand));
            var response = handle();
            return await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling create command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<TResult> CreateOnSyncResult<TCommand, TResponse, TEvent, TResult>(TCommand command,
        Func<string> getStreamId,
        Func<TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling create command {@Command}", typeof(TCommand));
            var response = handle();
            await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response.Event, ct);
            return response.Result;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling create command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<ulong> CreateOnAsync<TCommand, TResponse>(TCommand command,
        Func<string> getStreamId,
        Func<Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling create command {@Command}", typeof(TCommand));
            var response = await handle();
            return await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling create command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<ulong> CreateOnValueTaskAsync<TCommand, TResponse>(TCommand command,
        Func<string> getStreamId,
        Func<ValueTask<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling create command {@Command}", typeof(TCommand));
            var response = await handle();
            return await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling create command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<TResult> CreateOnAsyncResult<TCommand, TResponse, TEvent, TResult>(TCommand command,
        Func<string> getStreamId,
        Func<Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling create command {@Command}", typeof(TCommand));
            var response = await handle();
            await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response.Event, ct);
            return response.Result;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling create command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<TResult> CreateOnValueTaskAsyncResult<TCommand, TResponse, TEvent, TResult>(
        TCommand command,
        Func<string> getStreamId,
        Func<ValueTask<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling create command {@Command}", typeof(TCommand));
            var response = await handle();
            await ProcessCreateResponse(logger, serviceProvider, getStreamId, command, response.Event, ct);
            return response.Result;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling create command {@Command}", typeof(TCommand));
            throw;
        }
    }


    /// <summary>
    ///     Persists a create command handler response to <see cref="IEventStore" />
    /// </summary>
    /// <returns></returns>
    private static async Task<ulong> ProcessCreateResponse<TCommand, TResponse>(ILogger logger,
        IServiceProvider serviceProvider,
        Func<string> getStreamId,
        TCommand command,
        TResponse? response,
        CancellationToken ct)
        where TCommand : notnull
    {
        if (response == null)
            throw new InvalidOperationException($"Create Command {typeof(TCommand)} returned no response");
        var store = serviceProvider.GetRequiredService<IEventStore>();
        var enricher = serviceProvider.GetRequiredService<IEventEnricher>();
        object? metadata;
        ulong revision;
        var streamId = getStreamId();
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

        logger.LogInformation("Successfully handled create command {@Command}. Stream {StreamId} now at revision {Revision}",
            typeof(TCommand), getStreamId(), revision);
        return revision;
    }
}