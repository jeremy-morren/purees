namespace PureES;

/// <summary>
/// Specifies that a class contains PureES event handlers
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
[MeansImplicitUse(ImplicitUseTargetFlags.Members)]
public sealed class EventHandlersAttribute : Attribute
{
    
}