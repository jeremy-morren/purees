namespace PureES.Core;

/// <summary>
/// Specifies that a method is a command handler,
/// and that the parameter is the command
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
[PublicAPI]
public sealed class CommandAttribute : Attribute
{
}