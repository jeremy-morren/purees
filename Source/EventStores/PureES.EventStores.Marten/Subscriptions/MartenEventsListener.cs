﻿using System.Threading.Tasks.Dataflow;
using Marten;
using Marten.Services;
using PureES.Core;

namespace PureES.EventStores.Marten.Subscriptions;

internal class MartenEventsListener : DocumentSessionListenerBase, ISourceBlock<EventEnvelope>
{
    private readonly TransformManyBlock<CommittedEvent, EventEnvelope> _block;
    
    public MartenEventsListener(MartenEventSerializer serializer)
    {
        _block = new TransformManyBlock<CommittedEvent, EventEnvelope>(async c =>
            {
                //We need some kind of timeout
                var ct = new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token;
                
                await using var session = c.Store.QuerySession();
                return await session.Query<MartenEvent>()
                    .Where(e => e.StreamId == c.StreamId
                                && e.StreamPosition >= c.Start
                                && e.StreamPosition <= c.End)
                    .OrderBy(e => e.StreamPosition)
                    .ToAsyncEnumerable()
                    .Select(serializer.Deserialize)
                    .ToListAsync(ct);
            },
            new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true,
                
                //No backpressure on EventStore
                BoundedCapacity = DataflowBlockOptions.Unbounded,
                MaxDegreeOfParallelism = 1 //Ensure output is ordered as well
            });
    }
    
    public override Task AfterCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token)
    {
        /*
         * We have to reload them to get the timestamp
         * Going on the assumption that each commit includes only a single stream in order,
         * we can get the stream id, the low and the high stream position, and query with that
         */
        
        var list = commit.Inserted.OfType<MartenEvent>().ToList();
        
        if (list.Count == 0)
            return Task.CompletedTask;

        var e = new CommittedEvent(list[0].StreamId,
            list[0].StreamPosition,
            list[^1].StreamPosition,
            session.DocumentStore);
        
        //There is no backpressure, so this should succeed immediately
        if (!_block.Post(e))
            throw new InvalidOperationException("Failed to post events to event bus");
        return Task.CompletedTask;
    }

    private record CommittedEvent(string StreamId, int Start, int End, IDocumentStore Store);
    
    #region Dataflow block implementation

    public void Complete()
    {
        _block.Complete();
    }

    public void Fault(Exception exception)
    {
        ((IDataflowBlock)_block).Fault(exception);
    }

    public Task Completion => _block.Completion;

    public IDisposable LinkTo(ITargetBlock<EventEnvelope> target, DataflowLinkOptions linkOptions)
    {
        return _block.LinkTo(target, linkOptions);
    }

    public EventEnvelope? ConsumeMessage(DataflowMessageHeader messageHeader, 
        ITargetBlock<EventEnvelope> target, 
        out bool messageConsumed)
    {
        return ((ISourceBlock<EventEnvelope>)_block).ConsumeMessage(messageHeader, target, out messageConsumed);
    }

    public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<EventEnvelope> target)
    {
        return ((ISourceBlock<EventEnvelope>)_block).ReserveMessage(messageHeader, target);
    }

    public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<EventEnvelope> target)
    {
        ((ISourceBlock<EventEnvelope>)_block).ReleaseReservation(messageHeader, target);
    }
    
    #endregion
}