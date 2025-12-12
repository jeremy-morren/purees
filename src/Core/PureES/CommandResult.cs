using System.Collections;

namespace PureES;

/// <summary>
///     Represents the result of a command.
/// </summary>
/// <param name="Event">Command event. Can be an <see cref="IEnumerable" />.</param>
/// <typeparam name="TEvent">Command event type</typeparam>
/// <param name="Result">Command result</param>
/// <typeparam name="TResult">
///     Response type (corresponds to <c>TResult</c> in <see cref="ICommandHandler{TCommand, TResult}" />).
///     Can be nullable.
/// </typeparam>
/// <remarks>
/// <para>
///     Any response from a command handler other than this class will be treated as
///     an event and <see cref="ICommandHandler{TCommand}" /> will be registered.
/// </para>
/// <para>
///     If a handler returns this class,
///     <see cref="ICommandHandler{TCommand, TResult}" /> will be registered instead.
/// </para>
/// </remarks>
[PublicAPI]
public record CommandResult<TEvent, TResult>(TEvent Event, TResult Result);