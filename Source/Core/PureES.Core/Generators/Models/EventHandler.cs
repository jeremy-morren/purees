namespace PureES.Core.Generators.Models;

internal record EventHandler
{
    public required IType Parent { get; init; }

    public required IMethod Method { get; init; }

    public required IType Event { get; init; }

    public required IType[] Services { get; init; }
    
    /// <summary>
    /// Whether the return type is async
    /// </summary>
    public bool IsAsync => Method.ReturnType?.IsAsync(out _) ?? false;
    
    /// <summary>
    /// The handler method name
    /// </summary>
    public string HandlerMethodName
    {
        get
        {
            var str = Parent.FullName.Replace(".", string.Empty);
            str += $"_{Method.Name}";
            
            str = new[] { '+', '<', '>', '[', ']' }.Aggregate(str, (s, c) => s.Replace(c, '_'));
            return str;
        }
    }
}