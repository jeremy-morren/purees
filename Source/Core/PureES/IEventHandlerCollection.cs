namespace PureES;

/// <summary>
/// All event handlers registered for a given event type, ordered by priority.
/// </summary>
[PublicAPI]
public interface IEventHandlerCollection : IReadOnlyList<IEventHandler>
{
    /// <summary>
    /// The event type that this collection handles
    /// </summary>
    Type EventType { get; }
}