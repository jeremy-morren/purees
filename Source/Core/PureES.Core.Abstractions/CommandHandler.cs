using Microsoft.AspNetCore.Http;
using PureES.Core.EventStore;

namespace PureES.Core;

/// <summary>
/// Handles the given <c>Command</c>
/// </summary>
/// <typeparam name="TCommand">Command type</typeparam>
/// <returns>
/// The <c>StreamPosition</c> of the added event(s)
/// from <see cref="IEventStore"/>
/// </returns>
/// <remarks>
/// This handler is registered for a method that
/// does not return <see cref="CommandResult"/>
/// </remarks>
public delegate Task<ulong> CommandHandler<in TCommand>(TCommand arg, CancellationToken cancellationToken);

/// <summary>
/// Handles the given <c>Command</c>
/// </summary>
/// <typeparam name="TCommand">Command type</typeparam>
/// <typeparam name="TResult">Command result</typeparam>
/// <returns>
/// The Res
/// </returns>
/// <remarks>
/// This handler is registered for a method that
/// does not return <see cref="CommandResult"/>
/// </remarks>
public delegate Task<TResult> CommandHandler<in TCommand, TResult>(TCommand arg, CancellationToken cancellationToken);