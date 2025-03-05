namespace PureES;

/// <summary>
/// The exception that is thrown by <see cref="IAggregateFactory{TAggregate}"/>
/// when an error occurs rehydrating an aggregate
/// </summary>
[PublicAPI]
public class RehydrationException : Exception
{
    public IEventEnvelope Envelope { get; }
    public Type AggregateType { get; }

    public RehydrationException(IEventEnvelope envelope, Type aggregateType, Exception? innerException = null)
        : base(
            FormatMessage($"An error occurred rehydrating '{BasicEventTypeMap.GetTypeNames(aggregateType)[^1]}'", envelope),
            innerException)
    {
        Envelope = envelope;
        AggregateType = aggregateType;
    }
    
    public RehydrationException(IEventEnvelope envelope, Type aggregateType, string message, Exception? innerException = null)
        : base(
            FormatMessage($"{message}. Aggregate type: '{BasicEventTypeMap.GetTypeNames(aggregateType)[^1]}'", envelope),
            innerException)
    {
        Envelope = envelope;
        AggregateType = aggregateType;
    }
    
    private static string FormatMessage(string message, IEventEnvelope envelope) => 
        $"{message}. Stream Id: '{envelope.StreamId}'. Stream Pos: {envelope.StreamPosition}";
}