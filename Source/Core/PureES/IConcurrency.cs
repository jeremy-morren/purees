namespace PureES;

[PublicAPI]
public interface IConcurrency
{
    /// <summary>
    /// Gets the expected revision for a command
    /// </summary>
    /// <param name="streamId">The command stream</param>
    /// <param name="command">The command</param>
    /// <returns>
    /// The expected stream revision if available, otherwise <see langword="null" />
    /// </returns>
    ulong? GetExpectedRevision(string streamId, object command);
    
    /// <summary>
    /// Sets the expected revision for a stream after a command is handled
    /// </summary>
    /// <param name="streamId">The command stream</param>
    /// <param name="command">The handled command</param>
    /// <param name="previousRevision">The revision that the stream was at before the command was handled. <see langword="null"/> if stream did not exist.</param>
    /// <param name="currentRevision">The revision that the stream is currently at</param>
    void OnUpdated(string streamId, object command, ulong? previousRevision, ulong currentRevision);
}