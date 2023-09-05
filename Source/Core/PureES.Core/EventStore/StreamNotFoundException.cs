namespace PureES.Core.EventStore;

/// <summary>
/// The exception that is thrown when attempting to
/// read a stream that does not exist
/// </summary>
[PublicAPI]
public class StreamNotFoundException : Exception
{
    public StreamNotFoundException(string stream, Exception? innerException = null)
        : base("Event stream '" + stream + "' was not found", innerException) => 
        Stream = stream;

    public string Stream { get; }
}