using Microsoft.AspNetCore.Http;
using PureES.Core.EventStore;

namespace PureES.Core;

/// <summary>
/// Handles the given <c>Command</c>
/// </summary>
/// <typeparam name="TCommand">Command type</typeparam>
public interface ICommandHandler<in TCommand>
{
    /// <summary>
    /// Handles the given <c>Command</c>
    /// </summary>
    /// <returns>
    /// The <c>StreamPosition</c> of the added event(s)
    /// from <see cref="IEventStore"/>
    /// </returns>
    Task<ulong> Handle(TCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Handles the given <c>Command</c>
    /// </summary>
    /// <typeparam name="TResult">Result</typeparam>
    /// <returns>
    /// <see cref="CommandResult{TEvent,TResult}.Result"/> from <see cref="CommandResult{TEvent,TResult}"/>
    /// </returns>
    /// <remarks>
    /// This handler is only available for a method
    /// that returns <see cref="CommandResult{T,T}"/>
    /// </remarks>
    Task<TResult> Handle<TResult>(TCommand command, CancellationToken cancellationToken);
}