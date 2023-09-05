// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES.Core.EventStore;

/// <summary>
///     Exception thrown if the expected version specified when reading a stream
///     does not match the actual version of the stream when the operation was attempted.
/// </summary>
public class WrongStreamRevisionException : Exception
{
    public WrongStreamRevisionException(string streamName,
        ulong expectedRevision,
        ulong actualRevision,
        Exception? innerException = null)
        : base(
            $"Read failed due to WrongStreamRevision. Stream: '{streamName}', Expected revision: {expectedRevision}, Actual revision: {actualRevision}",
            innerException)
    {
        StreamName = streamName;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public string StreamName { get; }
    public ulong ExpectedRevision { get; }
    public ulong ActualRevision { get; }
}