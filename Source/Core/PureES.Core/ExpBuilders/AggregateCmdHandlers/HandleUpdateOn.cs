using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PureES.Core.EventStore;
using PureES.Core.ExpBuilders.Services;
using PureES.Core.ExpBuilders.WhenHandlers;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal static class HandleUpdateOn
{
    public static readonly MethodInfo UpdateOnSyncMethod = typeof(HandleUpdateOn).GetStaticMethod(nameof(UpdateOnSync));

    public static readonly MethodInfo UpdateOnAsyncMethod =
        typeof(HandleUpdateOn).GetStaticMethod(nameof(UpdateOnAsync));

    public static readonly MethodInfo UpdateOnValueTaskAsyncMethod =
        typeof(HandleUpdateOn).GetStaticMethod(nameof(UpdateOnValueTaskAsync));

    public static readonly MethodInfo UpdateOnSyncWithResultMethod =
        typeof(HandleUpdateOn).GetStaticMethod(nameof(UpdateOnSyncWithResult));

    public static readonly MethodInfo UpdateOnAsyncWithResultMethod =
        typeof(HandleUpdateOn).GetStaticMethod(nameof(UpdateOnAsyncWithResult));

    public static readonly MethodInfo UpdateOnValueTaskAsyncWithResultMethod =
        typeof(HandleUpdateOn).GetStaticMethod(nameof(UpdateOnValueTaskAsyncWithResult));

    public static async Task<ulong> UpdateOnSync<TAggregate, TCommand, TResponse>(TCommand command,
        Func<string> getStreamId,
        Func<IAsyncEnumerable<EventEnvelope>, ValueTask<LoadedAggregate<TAggregate>>> factory,
        Func<TAggregate, TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling update command {@Command}", typeof(TCommand));
            var streamId = getStreamId();
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var expectedVersion = await serviceProvider.GetExpectedVersion(command, ct);
            var events = expectedVersion != null
                ? store.Read(streamId, expectedVersion.Value, ct)
                : store.Read(streamId, ct);
            var current = await factory(events);
            var response = handle(current.Aggregate);
            return await ProcessUpdateResponse(logger, serviceProvider, streamId, current.Version, command, response,
                ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling update command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<ulong> UpdateOnValueTaskAsync<TAggregate, TCommand, TResponse>(TCommand command,
        Func<string> getStreamId,
        Func<IAsyncEnumerable<EventEnvelope>, ValueTask<LoadedAggregate<TAggregate>>> factory,
        Func<TAggregate, ValueTask<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling update command {@Command}", typeof(TCommand));
            var streamId = getStreamId();
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var expectedVersion = await serviceProvider.GetExpectedVersion(command, ct);
            var events = expectedVersion != null
                ? store.Read(streamId, expectedVersion.Value, ct)
                : store.Read(streamId, ct);
            var current = await factory(events);
            var response = await handle(current.Aggregate);
            return await ProcessUpdateResponse(logger,
                serviceProvider,
                streamId,
                current.Version,
                command,
                response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling update command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<ulong> UpdateOnAsync<TAggregate, TCommand, TResponse>(TCommand command,
        Func<string> getStreamId,
        Func<IAsyncEnumerable<EventEnvelope>, ValueTask<LoadedAggregate<TAggregate>>> factory,
        Func<TAggregate, Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling update command {@Command}", typeof(TCommand));
            var streamId = getStreamId();
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var expectedVersion = await serviceProvider.GetExpectedVersion(command, ct);
            var events = expectedVersion != null
                ? store.Read(streamId, expectedVersion.Value, ct)
                : store.Read(streamId, ct);
            var current = await factory(events);
            var response = await handle(current.Aggregate);
            return await ProcessUpdateResponse(logger,
                serviceProvider,
                streamId,
                current.Version,
                command,
                response, ct);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling update command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<TResult> UpdateOnSyncWithResult<TAggregate, TCommand, TResponse, TEvent, TResult>(
        TCommand command,
        Func<string> getStreamId,
        Func<IAsyncEnumerable<EventEnvelope>, ValueTask<LoadedAggregate<TAggregate>>> factory,
        Func<TAggregate, TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling update command {@Command}", typeof(TCommand));
            var streamId = getStreamId();
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var expectedVersion = await serviceProvider.GetExpectedVersion(command, ct);
            var events = expectedVersion != null
                ? store.Read(streamId, expectedVersion.Value, ct)
                : store.Read(streamId, ct);
            var current = await factory(events);
            var response = handle(current.Aggregate);
            await ProcessUpdateResponse(logger, serviceProvider, streamId, current.Version, command, response.Event, ct);
            return response.Result;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling update command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<TResult> UpdateOnAsyncWithResult<TAggregate, TCommand, TResponse, TEvent, TResult>(
        TCommand command,
        Func<string> getStreamId,
        Func<IAsyncEnumerable<EventEnvelope>, ValueTask<LoadedAggregate<TAggregate>>> factory,
        Func<TAggregate, Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling update command {@Command}", typeof(TCommand));
            var streamId = getStreamId();
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var expectedVersion = await serviceProvider.GetExpectedVersion(command, ct);
            var events = expectedVersion != null
                ? store.Read(streamId, expectedVersion.Value, ct)
                : store.Read(streamId, ct);
            var current = await factory(events);
            var response = await handle(current.Aggregate);
            await ProcessUpdateResponse(logger, serviceProvider, streamId, current.Version, command, response.Event, ct);
            return response.Result;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling update command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public static async Task<TResult> UpdateOnValueTaskAsyncWithResult<TAggregate, TCommand, TResponse, TEvent, TResult>(
        TCommand command,
        Func<string> getStreamId,
        Func<IAsyncEnumerable<EventEnvelope>, ValueTask<LoadedAggregate<TAggregate>>> factory,
        Func<TAggregate, ValueTask<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        try
        {
            logger.LogDebug("Handling update command {@Command}", typeof(TCommand));
            var streamId = getStreamId();
            var store = serviceProvider.GetRequiredService<IEventStore>();
            var expectedVersion = await serviceProvider.GetExpectedVersion(command, ct);
            var events = expectedVersion != null
                ? store.Read(streamId, expectedVersion.Value, ct)
                : store.Read(streamId, ct);
            var current = await factory(events);
            var response = await handle(current.Aggregate);
            await ProcessUpdateResponse(logger, serviceProvider, streamId, current.Version, command, response.Event, ct);
            return response.Result;
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Error handling update command {@Command}", typeof(TCommand));
            throw;
        }
    }


    /// <summary>
    ///     Persists an update command handler response to <see cref="IEventStore" />
    /// </summary>
    /// <returns></returns>
    public static async Task<ulong> ProcessUpdateResponse<TCommand, TResponse>(ILogger logger,
        IServiceProvider serviceProvider,
        string streamId,
        ulong currentVersion,
        TCommand command,
        TResponse? response,
        CancellationToken ct)
        where TCommand : notnull
    {
        var currentRevision = currentVersion - 1;
        if (response == null)
        {
            logger.LogInformation("Command {@Command} returned no result. Stream at revision {Revision}",
                typeof(TCommand), currentRevision);
            return currentRevision;
        }
        var store = serviceProvider.GetRequiredService<IEventStore>();
        var enricher = serviceProvider.GetRequiredService<IEventEnricher>();
        object? metadata;
        ulong revision;
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

        logger.LogInformation("Successfully handled update command {@Command}. Stream {StreamId} now at revision {Revision}",
            typeof(TCommand), streamId, revision);
        return revision;
    }
}