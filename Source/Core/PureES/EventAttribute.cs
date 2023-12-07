namespace PureES;

/// <summary>
/// Specifies that a method is an event handler,
/// and that the parameter is the event
/// </summary>
/// <remarks>
/// If the parameter inherits from <see cref="EventEnvelope{T,T}"/>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
[PublicAPI]
public sealed class EventAttribute : Attribute
{
}