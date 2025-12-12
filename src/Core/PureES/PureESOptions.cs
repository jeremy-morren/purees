using System.Threading.Tasks.Dataflow;

namespace PureES;

[PublicAPI]
public class PureESOptions
{
    /// <summary>
    /// Configure Event handlers
    /// </summary>
    public PureESEventHandlerOptions EventHandlers { get; } = new();

    /// <summary>
    /// Configure the event bus
    /// </summary>
    public ExecutionDataflowBlockOptions EventBusOptions { get; } = new()
    {
        EnsureOrdered = true
    };

    internal bool Validate() => EventHandlers.Validate();
}