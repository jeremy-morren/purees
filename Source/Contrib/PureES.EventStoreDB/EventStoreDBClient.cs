using System.Runtime.CompilerServices;
using EventStore.Client;
using PureES.Core;

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

    public async Task<bool> Exists(string streamId, CancellationToken cancellationToken)
    {
        var result = _eventStoreClient.ReadStreamAsync(Direction.Forwards, 
            streamId,
            StreamPosition.Start,
            1,
            cancellationToken: cancellationToken);
        return await result.ReadState == ReadState.Ok;
    }

    public async Task<ulong> Create(string streamId, 
        IEnumerable<UncommittedEvent> events, 
        CancellationToken cancellationToken)
    {
        var result = await _eventStoreClient.AppendToStreamAsync(streamId,
            StreamRevision.None,
            events.Select(_serializer.Serialize),
            cancellationToken: cancellationToken);
        return result.NextExpectedStreamRevision.ToUInt64();
    }

    public async Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        var result = await _eventStoreClient.AppendToStreamAsync(streamId,
            StreamRevision.None,
            new []{_serializer.Serialize(@event)},
            cancellationToken: cancellationToken);
        return result.NextExpectedStreamRevision.ToUInt64();
    }

    public async Task<ulong> Append(string streamId, ulong expectedRevision, IEnumerable<UncommittedEvent> events, CancellationToken cancellationToken)
    {
        var result = await _eventStoreClient.AppendToStreamAsync(streamId,
            StreamRevision.FromStreamPosition(expectedRevision),
            events.Select(_serializer.Serialize),
            cancellationToken: cancellationToken);
        return result.NextExpectedStreamRevision.ToUInt64();
    }

    public async Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event, CancellationToken cancellationToken)
    {
        var result = await _eventStoreClient.AppendToStreamAsync(streamId,
            StreamRevision.FromStreamPosition(expectedRevision),
            new []{_serializer.Serialize(@event)},
            cancellationToken: cancellationToken);
        return result.NextExpectedStreamRevision.ToUInt64();
    }

    public IAsyncEnumerable<EventEnvelope> Load(string streamId, CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards, 
            streamId, 
            revision: StreamPosition.Start,
            resolveLinkTos: true,
            cancellationToken: cancellationToken);
        return records.Select(r => _serializer.DeSerialize(r.Event));
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
        ulong count = 0;
        await foreach (var e in records.WithCancellation(cancellationToken))
        {
            yield return _serializer.DeSerialize(e.Event);
            count++;
            cancellationToken.ThrowIfCancellationRequested();
        }
        count--; //Make count 0-index based
        if (count != expectedRevision)
            throw new WrongStreamVersionException(streamId,
                new StreamRevision(expectedRevision),
                new StreamRevision(count));
    }

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
        var @events = records.Select(r => _serializer.DeSerialize(r.Event));
        ulong count = 0;
        await foreach (var e in @events.WithCancellation(cancellationToken))
        {
            yield return e;
            count++;
        }
        count--; //Make count 0-index based
        //If we get here, then we didn't reach requiredVersion
        if (count != requiredRevision)
            throw new WrongStreamVersionException(streamId,
                new StreamRevision(requiredRevision),
                new StreamRevision(count));
    }

    public async Task<ulong> Count(string streamId, CancellationToken cancellationToken)
    {
        var records = _eventStoreClient.ReadStreamAsync(Direction.Forwards, 
            streamId, 
            StreamPosition.Start,
            cancellationToken: cancellationToken);
        return (ulong) await records.LongCountAsync(cancellationToken);
    }

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
            yield return _serializer.DeSerialize(r.Event);
    }
}