

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES.Core.EventStore;

/// <summary>
/// Exception thrown if the expected version specified when reading a stream
/// does not match the actual version of the stream when the operation was attempted.
/// </summary>
public class WrongStreamVersionException : Exception
{
    public string StreamName { get; }
    public ulong ExpectedVersion { get; }
    public ulong ActualRevision { get; }

    public WrongStreamVersionException(string streamName,
        ulong expectedVersion,
        ulong actualRevision,
        Exception? innerException = null)
        : base($"Read failed due to WrongStreamVersion. Stream: '{streamName}', Expected version: {expectedVersion}, Actual version: {actualRevision}",
            innerException)
    {
        StreamName = streamName;
        ExpectedVersion = expectedVersion;
        ActualRevision = actualRevision;
    }
}