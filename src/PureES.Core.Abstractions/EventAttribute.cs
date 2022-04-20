namespace PureES.Core;

/// <summary>
/// Specifies that a method parameter
/// should be bound to a persisted event
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EventAttribute : Attribute { }