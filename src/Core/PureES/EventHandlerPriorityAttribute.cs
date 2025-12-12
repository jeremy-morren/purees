namespace PureES;

/// <summary>
/// Specify the handler priority (lower priority is executed first).
/// </summary>
/// <remarks>
/// Default (i.e. no attribute) priority is 0
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class EventHandlerPriorityAttribute : Attribute
{
    public int Priority { get; }

    public EventHandlerPriorityAttribute(int priority)
    {
        Priority = priority;
    }
}