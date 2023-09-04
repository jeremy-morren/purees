namespace PureES.Core.Generators.Models;

internal record EventHandler
{
    public required IType Parent { get; init; }

    public required IMethod Method { get; init; }

    public required IType Event { get; init; }

    public required IType[] Services { get; init; }
}