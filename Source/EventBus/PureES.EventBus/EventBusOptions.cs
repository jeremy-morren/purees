using System.Threading.Tasks.Dataflow;
using JetBrains.Annotations;

namespace PureES.EventBus;

[PublicAPI]
public class EventBusOptions
{
    /// <summary>
    ///     Indicates whether exceptions from <see cref="IEventBusEvents"/>
    ///     should be rethrown (defaults to <c>true</c>)
    /// </summary>
    /// <remarks>
    ///     When true, this will cause <see cref="IEventBusEvents"/> to throw any exceptions.
    ///     When false, exceptions will be caught and logged.
    /// </remarks>
    public bool PropagateEventBusExceptions { get; set; } = true;
    
    /// <summary>
    ///     Gets or sets the maximum number of event streams that may be processed simultaneously.
    ///     The default is <c>-1</c> (no limit)
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = DataflowBlockOptions.Unbounded;

    /// <summary>
    ///     Gets or sets the maximum number of events that may be buffered by <see cref="IEventBus"/>.
    ///     The default is <c>1</c>
    /// </summary>
    public int BufferSize { get; set; } = 1;

    internal void Validate()
    {
        if (BufferSize <= 0)
            throw new Exception($"{nameof(BufferSize)} must be greater than 0");
    }
}