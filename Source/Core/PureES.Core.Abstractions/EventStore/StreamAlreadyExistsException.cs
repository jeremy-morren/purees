using System.Diagnostics.CodeAnalysis;

namespace PureES.Core.EventStore;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class StreamAlreadyExistsException : Exception
{
    public StreamAlreadyExistsException(string stream, ulong currentRevision, Exception? innerException = null)
        : base("Event stream '" + stream + "' already exists", innerException)
    {
        Stream = stream;
        CurrentRevision = currentRevision;
    }

    public string Stream { get; }
    public ulong CurrentRevision { get; }
}