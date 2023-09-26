namespace PureES.Core.Generators.Models;

internal record When
{
    /// <summary>
    /// The underlying event type (not the envelope).
    /// If null, then the method receives non-generic EventEnvelope
    /// </summary>
    public required IType? Event { get; init; }
    
    public required IMethod Method { get; init; }

    public required bool IsUpdate { get; init; }
}