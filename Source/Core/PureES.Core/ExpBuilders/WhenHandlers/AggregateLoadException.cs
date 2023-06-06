namespace PureES.Core.ExpBuilders.WhenHandlers;

/// <summary>
/// The exception thrown when a <c>When</c> method on an aggregate throws
/// </summary>
public class AggregateLoadException : Exception
{
    public EventEnvelope Envelope { get; }
    public Type AggregateType { get; }

    internal AggregateLoadException(EventEnvelope @envelope, 
        Type aggregateType,
        Exception innerException)
        : base(
            $"An error occurred loading aggregate {aggregateType} for {@envelope.StreamId}/{@envelope.StreamPosition}",
            innerException)
    {
        Envelope = envelope;
        AggregateType = aggregateType;
    }
}