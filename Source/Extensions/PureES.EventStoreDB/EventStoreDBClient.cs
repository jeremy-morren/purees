using System.Runtime.CompilerServices;
using EventStore.Client;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.Core.EventStore.Serialization;
using PureES.EventStoreDB.Serialization;
using StreamNotFoundException = PureES.Core.EventStore.StreamNotFoundException;

namespace PureES.EventStoreDB;

internal class EventStoreDBClient : IEventStore
{
    private readonly EventStoreClient _eventStoreClient;
    private readonly IEventStoreDBSerializer _serializer;
    private readonly IEventTypeMap _typeMap;

    public EventStoreDBClient(EventStoreClient eventStoreClient,
        IEventStoreDBSerializer serializer,
        IEventTypeMap typeMap)
    {
        _eventStoreClient = eventStoreClient;
        _serializer = serializer;
        _typeMap = typeMap;
    }


    public async Task<bool> Exists(string streamId, CancellationToken cancellationToken)
    {
        var result = _eventStoreClient.ReadStreamAsync(Direction.Forwards,
            streamId,
            StreamPosition.Start,
            1,
            cancellationToken: cancellationToken);
        return await result.ReadState == ReadState.Ok;
    }

    public async Task<ulong> GetRevision(string streamId, CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Backwards,
            streamId,
            StreamPosition.End,
            1,
            cancellationToken: cancellationToken);
        if (await records.ReadState == ReadState.StreamNotFound)
            throw new StreamNotFoundException(streamId);
        var record = await records.FirstAsync(cancellationToken);
        return record.Event.EventNumber.ToUInt64();
    }

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
    public async Task<ulong> Append(string streamId, ulong expectedVersion, IEnumerable<UncommittedEvent> events,
        CancellationToken cancellationToken)
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
                throw new StreamNotFoundException(e.StreamName, e);
            throw new WrongStreamRevisionException(e.StreamName,
                e.ExpectedStreamRevision.ToUInt64(),
                e.ActualStreamRevision.ToUInt64());
        }
    }
    
    /// <inheritdoc />
    public async Task<ulong> Append(string streamId, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _eventStoreClient.AppendToStreamAsync(streamId,
                StreamState.StreamExists, 
                events.Select(_serializer.Serialize),
                cancellationToken: cancellationToken);
            return result.NextExpectedStreamRevision.ToUInt64();
        }
        catch (WrongExpectedVersionException e)
        {
            //We didn't provide a revision, so this is the only possibility
            throw new StreamNotFoundException(e.StreamName, e);
        }
    }

    /// <inheritdoc />
    public Task<ulong> Append(string streamId, ulong expectedVersion, UncommittedEvent @event,
        CancellationToken cancellationToken)
        => Append(streamId, expectedVersion, new[] {@event}, cancellationToken);
    
    /// <inheritdoc />
    public Task<ulong> Append(string streamId, UncommittedEvent @event,
        CancellationToken cancellationToken)
        => Append(streamId, new[] {@event}, cancellationToken);

    public async IAsyncEnumerable<EventEnvelope> ReadAll([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadAllAsync(Direction.Forwards,
            Position.Start,
            resolveLinkTos: true,
            cancellationToken: cancellationToken);
        await foreach (var record in records.WithCancellation(cancellationToken))
            yield return _serializer.Deserialize(record.Event);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EventEnvelope> Read(string streamId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards,
            streamId,
            StreamPosition.Start,
            resolveLinkTos: true,
            cancellationToken: cancellationToken);
        if (await records.ReadState == ReadState.StreamNotFound)
            throw new StreamNotFoundException(streamId);
        await foreach (var record in records.WithCancellation(cancellationToken))
            yield return _serializer.Deserialize(record.Event);
    }

    public async IAsyncEnumerable<EventEnvelope> Read(string streamId,
        ulong expectedRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards,
            streamId,
            StreamPosition.Start,
            resolveLinkTos: true,
            cancellationToken: cancellationToken);
        if (await records.ReadState == ReadState.StreamNotFound)
            throw new StreamNotFoundException(streamId);
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
            throw new WrongStreamRevisionException(streamId,
                new StreamRevision(expectedRevision),
                new StreamRevision(count));
    }

    public async IAsyncEnumerable<EventEnvelope> ReadPartial(string streamId,
        ulong requiredRevision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards,
            streamId,
            StreamPosition.Start,
            resolveLinkTos: true,
            maxCount: (long) (requiredRevision + 1),
            cancellationToken: cancellationToken);
        if (await records.ReadState == ReadState.StreamNotFound)
            throw new StreamNotFoundException(streamId);
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
            throw new WrongStreamRevisionException(streamId,
                new StreamRevision(requiredRevision),
                new StreamRevision(count));
    }

    public async IAsyncEnumerable<EventEnvelope> ReadByEventType(Type eventType,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        //See https://developers.eventstore.com/server/v21.10/projections.html#by-event-type
        var streamId = $"$et-{_typeMap.GetTypeName(eventType)}";
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards,
            streamId,
            StreamPosition.Start,
            resolveLinkTos: true,
            cancellationToken: cancellationToken);
        //If the stream isn't found, that means no events have been posted of the specified type
        //Hence we should return an empty stream
        if (await records.ReadState == ReadState.StreamNotFound)
            yield break;
        await foreach (var r in records.WithCancellation(cancellationToken))
            yield return _serializer.Deserialize(r.Event);
    }

    public async Task<ulong> Count(string streamId, CancellationToken cancellationToken)
    {
        //We will read the last event in the stream, and return the position
        var result = _eventStoreClient.ReadStreamAsync(Direction.Backwards,
            streamId,
            StreamPosition.End,
            maxCount: 1,
            resolveLinkTos: false,
            cancellationToken: cancellationToken);
        if (await result.ReadState == ReadState.StreamNotFound)
            throw new StreamNotFoundException(streamId);
        var e = await result.FirstAsync(cancellationToken);
        return e.Event.EventNumber.ToUInt64() + 1; //EventNumber is 0-based
    }
}