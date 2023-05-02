using System.Collections;

namespace PureES.Core;

/// <summary>
///     Represents the result of a command
/// </summary>
/// <param name="Event">Resultant events. Can be an <see cref="IEnumerable" /></param>
/// <param name="Result">Command result</param>
/// <typeparam name="TEvent">The event type to return. Can be nullable</typeparam>
/// <typeparam name="TResult">
///     Response type (corresponds to <c>TResult</c> in <see cref="ICommandHandler{TCommand}.Handle{TResult}" />).
///     Can be nullable.
/// </typeparam>
/// <remarks>
/// <para>
///     Any response from a command handler other than this class will be treated as
///     an event and <see cref="ICommandHandler{TCommand}.Handle" /> should be used.
/// </para>
/// <para>
///     If a handler returns this class,
///     <see cref="ICommandHandler{TCommand}.Handle{TResult}" /> should be used instead.
/// </para>
/// </remarks>
public record CommandResult<TEvent, TResult>(TEvent Event, TResult Result);