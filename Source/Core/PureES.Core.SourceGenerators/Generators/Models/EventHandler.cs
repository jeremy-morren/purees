namespace PureES.Core.SourceGenerators.Generators.Models;

internal record EventHandler
{
    public required IType Parent { get; init; }

    public required IMethod Method { get; init; }

    /// <summary>
    /// The type of event handled by the method. If null, then handler handles all events
    /// </summary>
    public required IType? EventType { get; init; }

    public required IType[] Services { get; init; }
    
    /// <summary>
    /// Whether the return type is async
    /// </summary>
    public bool IsAsync => Method.ReturnType?.IsAsync(out _) ?? false;
    
    /// <summary>
    /// The handler class name
    /// </summary>
    public string HandlerClassName
    {
        get
        {
            var name = Parent.FullName.Replace(".", string.Empty);
            name += $"_{Method.Name}";
            
            name = new[] { '+', '<', '>', '[', ']', '`' }.Aggregate(name, (s, c) => s.Replace(c, '_'));
            
            return $"{EventType?.Name}EventHandler_{name}";
        }
    }
}