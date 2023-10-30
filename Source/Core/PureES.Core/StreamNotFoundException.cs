namespace PureES.Core;

/// <summary>
/// The exception that is thrown when attempting to
/// read a stream that does not exist
/// </summary>
[PublicAPI]
public class StreamNotFoundException : Exception
{
    public StreamNotFoundException(string streamId, Exception? innerException = null)
        : base("Event stream '" + streamId + "' was not found", innerException) => 
        StreamId = streamId;

    public string StreamId { get; }
}