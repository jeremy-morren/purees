// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES.Core.EventStore;

public class StreamNotFoundException : Exception
{
    public StreamNotFoundException(string stream, Exception? innerException = null)
        : base("Event stream '" + stream + "' was not found", innerException)
        => Stream = stream;

    public string Stream { get; }
}