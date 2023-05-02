using System.Threading.Tasks.Dataflow;
using PureES.Core;

namespace PureES.EventBus;

/// <summary>
/// Publishes events to the registered EventHandlers (via <see cref="EventHandlerAttribute"/>)
/// </summary>
public interface IEventBus : ITargetBlock<EventEnvelope>
{
    
}