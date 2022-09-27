namespace PureES.Core.EventStore;

public class StreamNotFoundException : Exception
{
    public string Stream { get; }

    public StreamNotFoundException(string stream, Exception? innerException = null)
        : base("Event stream '" + stream + "' was not found", innerException)
        => Stream = stream;
}