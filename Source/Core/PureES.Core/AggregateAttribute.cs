namespace PureES.Core;

/// <summary>
///     Specifies that a record/class is
///     an EventSourcing root aggregate
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
[PublicAPI, MeansImplicitUse(ImplicitUseTargetFlags.WithMembers | ImplicitUseTargetFlags.Itself)]
public sealed class AggregateAttribute : Attribute
{
}