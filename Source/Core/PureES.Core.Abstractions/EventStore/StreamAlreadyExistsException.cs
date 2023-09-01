﻿namespace PureES.Core.EventStore;

/// <summary>
/// The exception that is thrown when attempting
/// to create a stream that already exists
/// </summary>
[PublicAPI]
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