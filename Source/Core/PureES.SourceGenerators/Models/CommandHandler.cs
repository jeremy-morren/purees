namespace PureES.SourceGenerators.Models;

internal record CommandHandler
{
    public required IType Command { get; init; }
    
    public required IMethod Method { get; init; }

    public required bool IsUpdate { get; init; }

    /// <summary>
    /// The stream id, if <see cref="Command"/> is decorated with <see cref="StreamIdAttribute"/>
    /// </summary>
    public required string? StreamId { get; init; }

    public required IType[] Services { get; init; }

    /// <summary>
    /// Whether the handler returns a <c>Task</c> or <c>ValueTask</c>
    /// </summary>
    public required bool IsAsync { get; init; }
    
    /// <summary>
    /// The actual return type of the handler (the underlying type of Task or ValueTask)
    /// </summary>
    public required IType ReturnType { get; init; }

    /// <summary>
    /// The event type that the handler returns.
    /// </summary>
    /// <remarks>
    /// If handler returns <see cref="CommandResult{TEvent, TResult}"/>, then <c>TEvent</c>
    /// Otherwise <see cref="ReturnType"/>
    /// </remarks>
    public required IType EventType { get; init; }
    
    /// <summary>
    /// The result type of the handler, if handler returns <see cref="CommandResult{TEvent,TResult}"/>
    /// </summary>
    public required IType? ResultType { get; init; }
}