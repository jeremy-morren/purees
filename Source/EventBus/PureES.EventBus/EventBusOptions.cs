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
}