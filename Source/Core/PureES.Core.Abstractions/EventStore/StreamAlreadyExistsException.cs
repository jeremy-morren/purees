namespace PureES.Core.EventStore;

public class StreamAlreadyExistsException : Exception
{
    public string Stream { get; }

    public StreamAlreadyExistsException(string stream, Exception? innerException = null)
        : base("Event stream '" + stream + "' already exists", innerException)
        => Stream = stream;
}