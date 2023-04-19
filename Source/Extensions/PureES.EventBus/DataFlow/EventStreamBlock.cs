using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using PureES.Core;

namespace PureES.EventBus.DataFlow;

/// <summary>
///     Dataflow block that processes event streams
///     in order (i.e. events in each stream are processed sequentially)
/// </summary>
public sealed class EventStreamBlock : ITargetBlock<EventEnvelope>
{
    private readonly Dictionary<string, EventQueue> _queues = new();

    private readonly ITargetBlock<EventEnvelope> _target;

    public EventStreamBlock(Func<EventEnvelope, CancellationToken, Task> action,
        EventStreamBlockOptions options)
    {
        var ct = options.CancellationToken;

        var producer = new TransformBlock<EventEnvelope, EventQueue>(e =>
        {
            ct.ThrowIfCancellationRequested();
            //We lock on _queues to allow removal below
            lock (_queues)
            {
                //Get or create queue
                if (!_queues.TryGetValue(e.StreamId, out var queue))
                {
                    queue = new EventQueue(e.StreamId,
                        new SemaphoreSlim(1, 1),
                        new ConcurrentQueue<EventEnvelope>());
                    _queues[e.StreamId] = queue;
                }

                queue.Queue.Enqueue(e);
                return queue;
            }
        }, new ExecutionDataflowBlockOptions
        {
            CancellationToken = ct,

            //Queue in the order we received, 1 at a time
            EnsureOrdered = true,
            MaxDegreeOfParallelism = 1,

            //This creates backpressure on block.SendAsync
            BoundedCapacity = options.BoundedCapacity
        });

        var worker = new TransformBlock<EventQueue, string>(async queue =>
        {
            //We must lock with the Semaphore to ensure only one thread on the queue at any time
            //However, if 2 events for a stream come in sequence, a worker will lock up just waiting on the Semaphore
            //Therefore we drop work here
            lock (queue)
            {
                if (queue.Mutex.CurrentCount == 0)
                    return queue.StreamId; //Another worker is operating below, we can skip to the end
            }

            //No-one waiting, so this should be very fast
            //We will save by skipping the async
            queue.Mutex.Wait(ct);
            try
            {
                //Note that there is a subtle race condition here: If we are already processing here,
                //we will grab the next event when it is enqueued above

                //As a result, we may get here to discover that all events have already
                //been processed by the previous queue (i.e. the queue is empty)

                //Not actually a problem, just something to note
                while (queue.Queue.TryDequeue(out var envelope))
                {
                    ct.ThrowIfCancellationRequested();
                    await action(envelope, ct);
                }
            }
            finally
            {
                queue.Mutex.Release();
            }

            return queue.StreamId;
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1, //Do not buffer anything, this creates backpressure
            CancellationToken = ct,

            //The worker process in parallel
            //Access to the streams is synchronized via the Semaphore
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,

            //Event streams are processed in no particular order
            EnsureOrdered = false
        });

        var cleanup = new ActionBlock<string>(streamId =>
        {
            ct.ThrowIfCancellationRequested();
            //Here, we remove empty queues
            lock (_queues)
            {
                if (!_queues.TryGetValue(streamId, out var queue))
                    return;
                if (queue.Queue.IsEmpty)
                    _queues.Remove(streamId);
            }
        }, new ExecutionDataflowBlockOptions
        {
            CancellationToken = ct,

            BoundedCapacity = DataflowBlockOptions.Unbounded, //The backpressure is all above, we can buffer as much as we like here
            EnsureOrdered = false, //Order is unimportant, since we are locking on _queues anyway
            MaxDegreeOfParallelism = 1 //We are synchronizing anyway, so this doesn't actually make any difference
        });

        producer.LinkTo(worker, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });
        worker.LinkTo(cleanup, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        _target = producer;

        Completion = cleanup.Completion;
    }

    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, EventEnvelope messageValue,
        ISourceBlock<EventEnvelope>? source, bool consumeToAccept) =>
        _target.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    public void Complete() => _target.Complete();

    public void Fault(Exception exception) => _target.Fault(exception);

    public Task Completion { get; }

    private record EventQueue(string StreamId, SemaphoreSlim Mutex, ConcurrentQueue<EventEnvelope> Queue);
}