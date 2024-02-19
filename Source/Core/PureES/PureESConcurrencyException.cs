namespace PureES;

/// <summary>
/// The exception that is thrown when a concurrency error occurs in <see cref="IEventStore"/>
/// </summary>
[PublicAPI]
public class PureESConcurrencyException : Exception
{
    public PureESConcurrencyException(Exception? innerException)
        : base("EventStore concurrency exception", innerException) {}
}