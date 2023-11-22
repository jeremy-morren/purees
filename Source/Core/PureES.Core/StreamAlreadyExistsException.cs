namespace PureES.Core;

/// <summary>
/// The exception that is thrown when attempting
/// to create a stream that already exists
/// </summary>
[PublicAPI]
public class StreamAlreadyExistsException : Exception
{
    public StreamAlreadyExistsException(string streamId, Exception? innerException = null)
        : base("Event stream '" + streamId + $"' already exists", innerException)
    {
        StreamId = streamId;
    }

    public string StreamId { get; }
    public ulong ActualRevision { get; }
}