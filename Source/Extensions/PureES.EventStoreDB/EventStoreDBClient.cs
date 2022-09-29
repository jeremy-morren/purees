using System.Runtime.CompilerServices;
using EventStore.Client;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.EventStoreDB.Serialization;
using StreamNotFoundException = EventStore.Client.StreamNotFoundException;

namespace PureES.EventStoreDB;

public class EventStoreDBClient : IEventStore
{
    private readonly EventStoreClient _eventStoreClient;
    private readonly IEventStoreDBSerializer _serializer;

    public EventStoreDBClient(EventStoreClient eventStoreClient,
        IEventStoreDBSerializer serializer)
    {
        _eventStoreClient = eventStoreClient;
        _serializer = serializer;
    }

    /// <inheritdoc />
    public async Task<bool> Exists(string streamId, CancellationToken cancellationToken)
    {
        var result = _eventStoreClient.ReadStreamAsync(Direction.Forwards, 
            streamId,
            StreamPosition.Start,
            1,
            cancellationToken: cancellationToken);
        return await result.ReadState == ReadState.Ok;
    }

    /// <inheritdoc />
    public async Task<ulong> GetRevision(string streamId, CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Backwards, 
            streamId, 
            revision: StreamPosition.End,
            maxCount: 1,
            cancellationToken: cancellationToken);
        if (await records.ReadState == ReadState.StreamNotFound)
            throw new Core.EventStore.StreamNotFoundException(streamId);
        var record = await records.FirstAsync(cancellationToken);
        return record.Event.EventNumber.ToUInt64();
    }

    /// <inheritdoc />
    public async Task<ulong> Create(string streamId, 
        IEnumerable<UncommittedEvent> events, 
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _eventStoreClient.AppendToStreamAsync(streamId,
                StreamRevision.None,
                events.Select(_serializer.Serialize),
                cancellationToken: cancellationToken);
            return result.NextExpectedStreamRevision.ToUInt64();
        }
        catch (WrongExpectedVersionException e) when (e.ExpectedVersion == null)
        {
            throw new StreamAlreadyExistsException(e.StreamName, e.ActualStreamRevision.ToUInt64(), e);
        }
    }

    /// <inheritdoc />
    public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken) =>
        Create(streamId, new[] {@event}, cancellationToken);

    /// <inheritdoc />
    public async Task<ulong> Append(string streamId, ulong expectedVersion, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _eventStoreClient.AppendToStreamAsync(streamId,
                StreamRevision.FromStreamPosition(expectedVersion),
                events.Select(_serializer.Serialize),
                cancellationToken: cancellationToken);
            return result.NextExpectedStreamRevision.ToUInt64();

        }
        catch (WrongExpectedVersionException e)
        {
            if (e.ActualVersion == null)
                throw new Core.EventStore.StreamNotFoundException(e.StreamName, e);
            throw new WrongStreamVersionException(e.StreamName, 
                e.ExpectedStreamRevision.ToUInt64(), 
                e.ActualStreamRevision.ToUInt64());
        }
    }

    /// <inheritdoc />
    public Task<ulong> Append(string streamId, ulong expectedVersion, UncommittedEvent @event, CancellationToken cancellationToken)
        => Append(streamId, expectedVersion, new[] {@event}, cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<EventEnvelope> Load(string streamId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards, 
            streamId, 
            revision: StreamPosition.Start,
            resolveLinkTos: true,
            cancellationToken: cancellationToken);
        if (await records.ReadState == ReadState.StreamNotFound)
            throw new Core.EventStore.StreamNotFoundException(streamId);
        await foreach (var record in records.WithCancellation(cancellationToken))
            yield return _serializer.Deserialize(record.Event);
    }

    public async IAsyncEnumerable<EventEnvelope> Load(string streamId,
        ulong expectedRevision, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards, 
            streamId, 
            revision: StreamPosition.Start,
            resolveLinkTos: true,
            cancellationToken: cancellationToken);
        if (await records.ReadState == ReadState.StreamNotFound)
            throw new Core.EventStore.StreamNotFoundException(streamId);
        ulong count = 0;
        await foreach (var e in records.WithCancellation(cancellationToken))
        {
            //If count > expectedRevision, save time by not returning
            //We will throw an exception later, so we are only counting
            if (count <= expectedRevision)
                yield return _serializer.Deserialize(e.Event);
            count++;
            cancellationToken.ThrowIfCancellationRequested();
        }
        count--; //Make count 0-index based
        if (count != expectedRevision)
            throw new WrongStreamVersionException(streamId,
                new StreamRevision(expectedRevision),
                new StreamRevision(count));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EventEnvelope> LoadPartial(string streamId, 
        ulong requiredRevision, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards, 
            streamId, 
            revision: StreamPosition.Start,
            resolveLinkTos: true,
            maxCount: (long)(requiredRevision + 1),
            cancellationToken: cancellationToken);
        if (await records.ReadState == ReadState.StreamNotFound)
            throw new Core.EventStore.StreamNotFoundException(streamId);
        ulong count = 0;
        await foreach (var e in records)
        {
            //If count > expectedRevision, we have loaded all events
            if (count > requiredRevision)
                yield break;
            yield return _serializer.Deserialize(e.Event);
            count++;
        }
        count--; //Make count 0-index based
        //If we get here, then we didn't reach requiredVersion
        if (count < requiredRevision)
            throw new WrongStreamVersionException(streamId,
                new StreamRevision(requiredRevision),
                new StreamRevision(count));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EventEnvelope> LoadByEventType(Type eventType, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        //See https://developers.eventstore.com/server/v21.10/projections.html#by-event-type
        var streamId = $"$et-{_serializer.GetTypeName(eventType)}";
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards, 
            streamId, 
            revision: StreamPosition.Start,
            resolveLinkTos: true,
            cancellationToken: cancellationToken);
        //If the stream isn't found, that means no events have been posted of the specified type
        //Hence we should return an empty stream
        if (await records.ReadState == ReadState.StreamNotFound)
            yield break;
        await foreach (var r in records.WithCancellation(cancellationToken))
            yield return _serializer.Deserialize(r.Event);
    }
}