using EventStore.Client;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES.EventStoreDB;

/// <summary>
/// Exception thrown if the expected version specified when reading a stream
/// does not match the actual version of the stream when the operation was attempted.
/// </summary>
public class WrongStreamVersionException : Exception
{
    public string StreamName { get; }
    public StreamRevision ExpectedRevision { get; }
    public StreamRevision ActualRevision { get; }

    public WrongStreamVersionException(string streamName,
        StreamRevision expectedRevision,
        StreamRevision actualRevision)
        : base($"Read failed due to WrongStreamVersion. Stream: {streamName}, Expected version: {expectedRevision}, Actual version: {actualRevision}")
    {
        StreamName = streamName;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }
}