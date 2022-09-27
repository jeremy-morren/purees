using Microsoft.AspNetCore.Http;

namespace PureES.Core;

/// <summary>
/// Handles the given <c>Command</c>
/// and returns the <c>StreamPosition</c>
/// from <see cref="IEventStore"/>
/// </summary>
/// <typeparam name="T">Command type</typeparam>
/// <remarks>
/// If <paramref name="cancellationToken"/> is null
/// then <see cref="HttpContext"/>.<see cref="HttpContext.RequestAborted"/>
/// will be used instead
/// </remarks>
public delegate Task<ulong> CommandHandler<in T>(T arg, CancellationToken cancellationToken);