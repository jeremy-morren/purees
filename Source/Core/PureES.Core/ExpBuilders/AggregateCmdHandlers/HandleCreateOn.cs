using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PureES.Core.EventStore;

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
        string streamId,
        Func<TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : notnull
    {
        var response = handle();
        return await ProcessCreateResponse(serviceProvider, streamId, command, response, ct);
    }

    public static async Task<TResult> CreateOnSyncResult<TCommand, TResponse, TEvent, TResult>(TCommand command,
        string streamId,
        Func<TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var response = handle();
        await ProcessCreateResponse(serviceProvider, streamId, command, response.Event, ct);
        return response.Result;
    }

    public static async Task<ulong> CreateOnAsync<TCommand, TResponse>(TCommand command,
        string streamId,
        Func<Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
    {
        var response = await handle();
        return await ProcessCreateResponse(serviceProvider, streamId, command, response, ct);
    }

    public static async Task<ulong> CreateOnValueTaskAsync<TCommand, TResponse>(TCommand command,
        string streamId,
        Func<ValueTask<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
    {
        var response = await handle();
        return await ProcessCreateResponse(serviceProvider, streamId, command, response, ct);
    }

    public static async Task<TResult> CreateOnAsyncResult<TCommand, TResponse, TEvent, TResult>(TCommand command,
        string streamId,
        Func<Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var response = await handle();
        await ProcessCreateResponse(serviceProvider, streamId, command, response.Event, ct);
        return response.Result;
    }

    public static async Task<TResult> CreateOnValueTaskAsyncResult<TCommand, TResponse, TEvent, TResult>(
        TCommand command,
        string streamId,
        Func<ValueTask<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var response = await handle();
        await ProcessCreateResponse(serviceProvider, streamId, command, response.Event, ct);
        return response.Result;
    }


    /// <summary>
    ///     Persists a create command handler response to <see cref="IEventStore" />
    /// </summary>
    /// <returns></returns>
    private static async Task<ulong> ProcessCreateResponse<TCommand, TResponse>(
        IServiceProvider serviceProvider,
        string streamId,
        TCommand command,
        TResponse? response,
        CancellationToken ct)
        where TCommand : notnull
    {
        if (response == null)
            throw new InvalidOperationException($"Create command {typeof(TCommand)} returned no response");
        var store = serviceProvider.GetRequiredService<IEventStore>();
        var enricher = serviceProvider.GetRequiredService<IEventEnricher>();
        object? metadata;
        ulong revision;
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        if (response is IEnumerable enumerable)
        {
            var events = new List<UncommittedEvent>();
            foreach (var e in enumerable)
            {
                metadata = await enricher.GetMetadata(command, e, ct);
                events.Add(new UncommittedEvent(Guid.NewGuid(), e, metadata));
            }

            revision = await store.Create(streamId, events, ct);
            
            logger.LogDebug("Appended {EventCount} event(s). Stream {StreamId} now at revision {Revision}",
                events.Count, streamId, revision);
        }
        else
        {
            metadata = await enricher.GetMetadata(command, response, ct);
            var @event = new UncommittedEvent(Guid.NewGuid(), response, metadata);
            revision = await store.Create(streamId, @event, ct);
            
            logger.LogDebug("Appended {EventCount} event(s). Stream {StreamId} now at revision {Revision}",
                1, streamId, revision);
        }
        return revision;
    }
}