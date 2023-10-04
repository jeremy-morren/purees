namespace PureES.Core.EventStore;

/// <summary>
/// Extracts the stream id from <typeparamref name="TCommand"/>
/// </summary>
/// <typeparam name="TCommand">The command type</typeparam>
/// <remarks>
/// This interface is not used if command is decorated with <see cref="StreamIdAttribute"/>
/// </remarks>
public interface ICommandStreamId<in TCommand>
{
    /// <summary>
    /// Extracts the stream id from <paramref name="command"/>
    /// </summary>
    /// <param name="command">The command to extract the stream id from</param>
    /// <returns>The command stream ID</returns>
    string GetStreamId(TCommand command);
}