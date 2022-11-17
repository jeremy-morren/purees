using System.Threading.Tasks.Dataflow;

namespace PureES.EventBus.DataFlow;

public class EventStreamBlockOptions
{
    /// <summary>
    /// Gets the maximum number of event streams that may be processed simultaneously.
    /// The default is <c>-1</c> (no limit)
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of events that may be buffered by the block.
    /// </summary>
    public int BoundedCapacity { get; set; } = DataflowBlockOptions.Unbounded;

    /// <summary>Gets or sets the <see cref="CancellationToken" /> to monitor for cancellation requests.</summary>
    /// <returns>The token.</returns>
    public CancellationToken CancellationToken { get; set; } = default;
}