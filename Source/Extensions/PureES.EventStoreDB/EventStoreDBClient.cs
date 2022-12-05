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
    
    public async Task<ulong> GetRevision(string streamId, ulong expectedRevision, CancellationToken cancellationToken)
    {
        var actual = await GetRevision(streamId, cancellationToken);
        if (actual != expectedRevision)
            throw new WrongStreamRevisionException(streamId, expectedRevision, actual);
        return actual;
    }
    
    #region Append

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
    public async Task<ulong> Append(string streamId, ulong expectedRevision, IEnumerable<UncommittedEvent> events,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _eventStoreClient.AppendToStreamAsync(streamId,
                StreamRevision.FromStreamPosition(expectedRevision),
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
    public Task<ulong> Append(string streamId, ulong expectedRevision, UncommittedEvent @event,
        CancellationToken cancellationToken)
        => Append(streamId, expectedRevision, new[] {@event}, cancellationToken);
    
    /// <inheritdoc />
    public Task<ulong> Append(string streamId, UncommittedEvent @event,
        CancellationToken cancellationToken)
        => Append(streamId, new[] {@event}, cancellationToken);
    
    #endregion
    
    #region Read

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

    #endregion
    
    #region Read Multiple

    public IAsyncEnumerable<EventEnvelope> ReadMany(IEnumerable<string> streams, 
        CancellationToken cancellationToken)
    {
        //Ensure we get only distinct streams
        var enumerators = streams.Distinct().SelectAwait(async stream =>
        {
            var readResult = _eventStoreClient.ReadStreamAsync(direction: Direction.Forwards,
                streamName: stream,
                revision: StreamPosition.Start,
                resolveLinkTos: true,
                cancellationToken: cancellationToken);
            if (await readResult.ReadState == ReadState.StreamNotFound)
                throw new StreamNotFoundException(stream);
            return readResult;
        });
        return Merge(enumerators, cancellationToken);
    }

    public IAsyncEnumerable<EventEnvelope> ReadMany(IAsyncEnumerable<string> streams, CancellationToken cancellationToken)
    {
        //Ensure we get only distinct streams
        var enumerators = streams.Distinct().SelectAwait(async stream =>
        {
            var readResult = _eventStoreClient.ReadStreamAsync(direction: Direction.Forwards,
                streamName: stream,
                revision: StreamPosition.Start,
                resolveLinkTos: true,
                cancellationToken: cancellationToken);
            if (await readResult.ReadState == ReadState.StreamNotFound)
                throw new StreamNotFoundException(stream);
            return readResult;
        });
        return Merge(enumerators, cancellationToken);
    }

    /// <summary>
    /// Merges multiple streams together, returning events in chronological order
    /// </summary>
    private async IAsyncEnumerable<EventEnvelope> Merge(IAsyncEnumerable<IAsyncEnumerable<ResolvedEvent>> streams,
        [EnumeratorCancellation] CancellationToken ct)
    {
        //We will maintain a dictionary of timestamps to enumerators
        //These represent the last event read from each stream
        
        //Read the next event from the stream with the earliest event
        //Repeat until all enumerators complete

        var records = new SortedDictionary<DateTime, IAsyncEnumerator<EventRecord>>();
        
        try
        {
            //Seed the dictionary with the first result from each stream

            await foreach (var stream in streams.WithCancellation(ct))
            {
                var enumerator = stream.Select(e => e.Event).GetAsyncEnumerator(ct);
                try
                {
                    if (!await enumerator.MoveNextAsync(ct))
                        //The stream was empty, ignore
                        await enumerator.DisposeAsync();
                    else
                        records.Add(enumerator.Current.Created, enumerator);
                }
                catch (Exception)
                {
                    await enumerator.DisposeAsync();
                    throw;
                }
            }

            //Loop until all streams are completed
            while (records.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                
                var date = records.Keys.First();
                var stream = records[date];
            
                //return the Current Event (i.e. the one with timestamp date)
                yield return _serializer.Deserialize(stream.Current);
            
                records.Remove(date);
                try
                {
                    if (!await stream.MoveNextAsync(ct))
                        //We have reached the end of the stream, Dispose
                        await stream.DisposeAsync();
                    else
                        //Update stream timestamp
                        records.Add(stream.Current.Created, stream);
                }
                catch (Exception)
                {
                    await stream.DisposeAsync();
                    throw;
                }
            }
        }
        finally
        {
            //Allowing for the fact that things may go wrong, ensure we dispose the streams
            foreach (var stream in records.Values)
                await stream.DisposeAsync();
        }
    }

    #endregion
}