// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES;

/// <summary>
///     Exception thrown if the expected version specified when reading a stream
///     does not match the actual version of the stream when the operation was attempted.
/// </summary>
public class WrongStreamRevisionException : Exception
{
    public WrongStreamRevisionException(string streamId,
        uint expectedRevision,
        uint actualRevision,
        Exception? innerException = null)
        : base(
            $"Operation failed due to WrongStreamRevision. Stream: '{streamId}', Expected revision: {expectedRevision}, Actual revision: {actualRevision}",
            innerException)
    {
        StreamId = streamId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public string StreamId { get; }
    public uint ExpectedRevision { get; }
    public uint ActualRevision { get; }
}