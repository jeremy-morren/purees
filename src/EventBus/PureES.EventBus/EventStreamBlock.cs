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
    private readonly Dictionary<string, StreamQueue> _queues = new();

    private readonly ITargetBlock<EventEnvelope> _producer;
    
    private readonly ISourceBlock<EventEnvelope> _consumer;

    public EventStreamBlock(Func<EventEnvelope, Task> handle, ExecutionDataflowBlockOptions options)
    {
        //Producer creates/appends to queues
        var producer = new TransformBlock<EventEnvelope, StreamQueue>(e =>
        {
            //We lock on _queues to allow removal below
            lock (_queues)
            {
                //Get or create queue
                if (!_queues.TryGetValue(e.StreamId, out var queue))
                {
                    queue = new StreamQueue(
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

            CancellationToken = options.CancellationToken,

            //Apply backpressure here if required
            BoundedCapacity = options.BoundedCapacity,
            TaskScheduler = options.TaskScheduler,
            NameFormat = options.NameFormat,
            MaxMessagesPerTask = options.MaxMessagesPerTask
        });

        //The worker block is parallel
        //Access to the streams is synchronized via the Semaphore
        
        var worker = new TransformManyBlock<StreamQueue, EventEnvelope>(async queue =>
        {
            await queue.Mutex.WaitAsync();
            try
            {
                if (queue.Queue.TryDequeue(out var envelope))
                {
                    await handle(envelope);
                    return [envelope];
                }

                //No events in the queue: should not happen, but just in case
                return [];
            }
            finally
            {
                queue.Mutex.Release();
            }
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1,

            //Event streams are processed in no particular order
            EnsureOrdered = false,

            //Configure processor with provided parallelism
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            TaskScheduler = options.TaskScheduler,
            NameFormat = options.NameFormat,
            MaxMessagesPerTask = options.MaxMessagesPerTask
        });

        var cleanup = new TransformBlock<EventEnvelope, EventEnvelope>(envelope =>
            {
                //Here, we remove empty queues
                lock (_queues)
                {
                    if (_queues.TryGetValue(envelope.StreamId, out var queue) && queue.Queue.IsEmpty)
                        _queues.Remove(envelope.StreamId);
                }
                return envelope;
            },
            new ExecutionDataflowBlockOptions
            {
                //Order & synchronization are unimportant, since we are locking on _queues anyway
                EnsureOrdered = false, 
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,

                TaskScheduler = options.TaskScheduler,
                NameFormat = options.NameFormat,
                MaxMessagesPerTask = options.MaxMessagesPerTask
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

    /// <summary>
    /// A queue of events for a stream
    /// </summary>
    private record StreamQueue(SemaphoreSlim Mutex, ConcurrentQueue<EventEnvelope> Queue);

    
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