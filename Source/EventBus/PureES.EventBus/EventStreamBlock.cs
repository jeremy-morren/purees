using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace PureES.EventBus;

/// <summary>
///     Dataflow block that processes event streams
///     in order (i.e. events in each stream are processed sequentially).
///     Propagates events after they are handled.
/// </summary>
internal sealed class EventStreamBlock : ITargetBlock<EventEnvelope>, ISourceBlock<EventEnvelope>
{
    private readonly Dictionary<string, EventQueue> _queues = new();

    private readonly ITargetBlock<EventEnvelope> _producer;
    
    private readonly ISourceBlock<EventEnvelope> _consumer;

    public EventStreamBlock(Func<EventEnvelope, Task> handle,
        EventBusOptions options)
    {
        options.Validate();
        //Producer creates/appends to queues
        var producer = new TransformBlock<EventEnvelope, EventQueue>(e =>
        {
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
            //Queue in the order we received, 1 at a time
            EnsureOrdered = true,
            MaxDegreeOfParallelism = 1,

            //This creates backpressure on block.SendAsync
            BoundedCapacity = options.BufferSize
        });

        var worker = new TransformBlock<EventQueue, HandleResult>(async queue =>
        {
            var handled = new List<EventEnvelope>();
            
            //We must lock with the Semaphore to ensure only one thread on the queue at any time
            //However, if 2 events for a stream come in sequence, a worker will lock up just waiting on the Semaphore
            //Therefore we drop work here
            lock (queue)
            {
                if (queue.Mutex.CurrentCount == 0)
                    return new HandleResult(queue.StreamId, handled); //Another worker is operating on this stream below, we can skip to the end
            }

            //No-one waiting, so this should be very fast
            //We will save by skipping the async
            queue.Mutex.Wait();
            try
            {
                //Note that there is a subtle race condition here: If we are already processing here,
                //we will grab the next event when it is enqueued above

                //As a result, we may get here to discover that all events have already
                //been processed by the previous queue (i.e. the queue is empty)

                //Not actually a problem, just something to note
                while (queue.Queue.TryDequeue(out var envelope))
                {
                    await handle(envelope);
                    handled.Add(envelope);
                }
            }
            finally
            {
                queue.Mutex.Release();
            }

            return new HandleResult(queue.StreamId, handled);
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1, //Do not buffer anything, this creates backpressure

            //The worker process in parallel
            //Access to the streams is synchronized via the Semaphore
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,

            //Event streams are processed in no particular order
            EnsureOrdered = false
        });

        var cleanup = new TransformManyBlock<HandleResult, EventEnvelope>(result =>
            {
                //Here, we remove empty queues
                lock (_queues)
                {
                    if (_queues.TryGetValue(result.StreamId, out var queue) && queue.Queue.IsEmpty)
                        _queues.Remove(result.StreamId);
                }
                return result.Handled;
            },
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = DataflowBlockOptions.Unbounded, //The backpressure is all above, we can buffer as much as we like here
                
                //Order & synchronization are unimportant, since we are locking on _queues anyway
                EnsureOrdered = false, 
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded
            });

        producer.LinkTo(worker, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });
        worker.LinkTo(cleanup, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        _producer = producer;
        _consumer = cleanup;
    }
    
    private record EventQueue(string StreamId, SemaphoreSlim Mutex, ConcurrentQueue<EventEnvelope> Queue);
    
    private record HandleResult(string StreamId, List<EventEnvelope> Handled);
    
    #region DataFlow Implementation

    //Producer
    
    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, EventEnvelope messageValue,
        ISourceBlock<EventEnvelope>? source, bool consumeToAccept) =>
        _producer.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    public void Complete() => _producer.Complete();

    public void Fault(Exception exception) => _producer.Fault(exception);
    
    public Task Completion => _consumer.Completion; //Wait on consumer to complete
    
    //Consumer
    public EventEnvelope? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<EventEnvelope> target, out bool messageConsumed) => _consumer.ConsumeMessage(messageHeader, target, out messageConsumed);

    public IDisposable LinkTo(ITargetBlock<EventEnvelope> target, DataflowLinkOptions linkOptions) => _consumer.LinkTo(target, linkOptions);

    public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<EventEnvelope> target)
    {
        _consumer.ReleaseReservation(messageHeader, target);
    }

    public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<EventEnvelope> target) => _consumer.ReserveMessage(messageHeader, target);

    #endregion
}