using System.Collections;
using PureES.Core.EventStore;

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
        string streamId,
        Func<TAggregate, TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TAggregate : notnull
        where TCommand : notnull
    {
        var eventStore = serviceProvider.GetRequiredService<IEventStore>();
        var aggregateStore = serviceProvider.GetRequiredService<IAggregateStore<TAggregate>>();
        var expectedRevision = await serviceProvider.GetExpectedRevision(command, ct);
        var streamRevision = expectedRevision.HasValue
            ? await eventStore.GetRevision(streamId, expectedRevision.Value, ct)
            : await eventStore.GetRevision(streamId, ct);
        var current = await aggregateStore.Load(streamId, streamRevision, ct);
        var response = handle(current.Aggregate);
        return await ProcessUpdateResponse(serviceProvider, streamId, streamRevision, command, response, ct);
    }

    public static async Task<ulong> UpdateOnValueTaskAsync<TAggregate, TCommand, TResponse>(TCommand command,
        string streamId,
        Func<TAggregate, ValueTask<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TAggregate : notnull
        where TCommand : notnull
    {
        var eventStore = serviceProvider.GetRequiredService<IEventStore>();
        var aggregateStore = serviceProvider.GetRequiredService<IAggregateStore<TAggregate>>();
        var expectedRevision = await serviceProvider.GetExpectedRevision(command, ct);
        var streamRevision = expectedRevision.HasValue
            ? await eventStore.GetRevision(streamId, expectedRevision.Value, ct)
            : await eventStore.GetRevision(streamId, ct);
        var current = await aggregateStore.Load(streamId, streamRevision, ct);
        var response = await handle(current.Aggregate);
        return await ProcessUpdateResponse(serviceProvider, streamId, streamRevision, command, response, ct);
    }

    public static async Task<ulong> UpdateOnAsync<TAggregate, TCommand, TResponse>(TCommand command,
        string streamId,
        Func<TAggregate, Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TAggregate : notnull
        where TCommand : notnull
    {
        var eventStore = serviceProvider.GetRequiredService<IEventStore>();
        var aggregateStore = serviceProvider.GetRequiredService<IAggregateStore<TAggregate>>();
        var expectedRevision = await serviceProvider.GetExpectedRevision(command, ct);
        var streamRevision = expectedRevision.HasValue
            ? await eventStore.GetRevision(streamId, expectedRevision.Value, ct)
            : await eventStore.GetRevision(streamId, ct);
        var current = await aggregateStore.Load(streamId, streamRevision, ct);
        var response = await handle(current.Aggregate);
        return await ProcessUpdateResponse(serviceProvider, streamId, streamRevision, command, response, ct);
    }

    public static async Task<TResult> UpdateOnSyncWithResult<TAggregate, TCommand, TResponse, TEvent, TResult>(
        TCommand command,
        string streamId,
        Func<TAggregate, TResponse> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TAggregate : notnull
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var eventStore = serviceProvider.GetRequiredService<IEventStore>();
        var aggregateStore = serviceProvider.GetRequiredService<IAggregateStore<TAggregate>>();
        var expectedRevision = await serviceProvider.GetExpectedRevision(command, ct);
        var streamRevision = expectedRevision.HasValue
            ? await eventStore.GetRevision(streamId, expectedRevision.Value, ct)
            : await eventStore.GetRevision(streamId, ct);
        var current = await aggregateStore.Load(streamId, streamRevision, ct);
        var response = handle(current.Aggregate);
        await ProcessUpdateResponse(serviceProvider, streamId, streamRevision, command, response.Event, ct);
        return response.Result;
    }

    public static async Task<TResult> UpdateOnAsyncWithResult<TAggregate, TCommand, TResponse, TEvent, TResult>(
        TCommand command,
        string streamId,
        Func<TAggregate, Task<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TAggregate : notnull
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var eventStore = serviceProvider.GetRequiredService<IEventStore>();
        var aggregateStore = serviceProvider.GetRequiredService<IAggregateStore<TAggregate>>();
        var expectedRevision = await serviceProvider.GetExpectedRevision(command, ct);
        var streamRevision = expectedRevision.HasValue
            ? await eventStore.GetRevision(streamId, expectedRevision.Value, ct)
            : await eventStore.GetRevision(streamId, ct);
        var current = await aggregateStore.Load(streamId, streamRevision, ct);
        var response = await handle(current.Aggregate);
        await ProcessUpdateResponse(serviceProvider, streamId, streamRevision, command, response.Event, ct);
        return response.Result;
    }

    public static async Task<TResult> UpdateOnValueTaskAsyncWithResult<TAggregate, TCommand, TResponse, TEvent, TResult>(
        TCommand command,
        string streamId,
        Func<TAggregate, ValueTask<TResponse>> handle,
        IServiceProvider serviceProvider,
        CancellationToken ct)
        where TAggregate : notnull
        where TCommand : notnull
        where TResponse : CommandResult<TEvent, TResult>
    {
        var eventStore = serviceProvider.GetRequiredService<IEventStore>();
        var aggregateStore = serviceProvider.GetRequiredService<IAggregateStore<TAggregate>>();
        var expectedRevision = await serviceProvider.GetExpectedRevision(command, ct);
        var streamRevision = expectedRevision.HasValue
            ? await eventStore.GetRevision(streamId, expectedRevision.Value, ct)
            : await eventStore.GetRevision(streamId, ct);
        var current = await aggregateStore.Load(streamId, streamRevision, ct);
        var response = await handle(current.Aggregate);
        await ProcessUpdateResponse(serviceProvider, streamId, streamRevision, command, response.Event, ct);
        return response.Result;
    }


    /// <summary>
    ///     Persists an update command handler response to <see cref="IEventStore" />
    /// </summary>
    /// <returns></returns>
    public static async Task<ulong> ProcessUpdateResponse<TCommand, TResponse>(
        IServiceProvider serviceProvider,
        string streamId,
        ulong currentRevision,
        TCommand command,
        TResponse? response,
        CancellationToken ct)
        where TCommand : notnull
    {
        var logger = CommandServicesBuilder.GetLogger(serviceProvider);
        if (response == null)
        {
            logger.LogInformation("Update command {@Command} returned no result. Stream at revision {Revision}",
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
            
            
            logger.LogDebug("Appended {EventCount} event(s). Stream {StreamId} now at revision {Revision}",
                events.Count, streamId, revision);
        }
        else
        {
            metadata = await enricher.GetMetadata(command, response, ct);
            var @event = new UncommittedEvent(Guid.NewGuid(), response, metadata);
            revision = await store.Append(streamId, currentRevision, @event, ct);
            
            
            logger.LogDebug("Appended {EventCount} event(s). Stream {StreamId} now at revision {Revision}",
                1, streamId, revision);
        }
        return revision;
    }
}