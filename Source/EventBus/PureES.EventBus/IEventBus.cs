using System.Threading.Tasks.Dataflow;

namespace PureES.EventBus;

/// <summary>
/// Publishes events to the registered EventHandlers (via <see cref="EventHandlersAttribute"/>)
/// </summary>
public interface IEventBus : ITargetBlock<EventEnvelope>
{
    
}