namespace PureES.SourceGenerators.Models;

internal record When
{
    /// <summary>
    /// The underlying event type (not the envelope).
    /// If null, then the method receives non-generic EventEnvelope
    /// </summary>
    public required IType? Event { get; init; }
    
    public required IMethod Method { get; init; }

    public required bool IsUpdate { get; init; }
    
    public required IType[] Services { get; init; }

    public bool IsAsync => Method.ReturnType != null && Method.ReturnType.IsAsync(out _);

    /// <summary>
    /// Gets the order that the when method should be called.
    /// </summary>
    /// <returns></returns>
    public int GetInheritanceDepth() => GetInheritanceDepth(Event);

    private static int GetInheritanceDepth(IType? type)
    {
        if (type == null)
            return -1;
        var depth = 0;
        while (true)
        {
            if (type.BaseType == null)
                return depth;
            depth++;
            type = type.BaseType;
        }
    }
}