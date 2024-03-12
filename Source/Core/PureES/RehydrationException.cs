namespace PureES;

/// <summary>
/// The exception that is thrown by <see cref="IAggregateFactory{TAggregate}"/>
/// when an error occurs rehydrating an aggregate
/// </summary>
[PublicAPI]
public class RehydrationException : Exception
{
    public string StreamId { get; }
    public Type AggregateType { get; }

    public RehydrationException(string streamId, 
        Type aggregateType, 
        Exception innerException)
        : base($"An error occurred rehydrating '{BasicEventTypeMap.GetTypeName(aggregateType)}'. Stream Id: '{streamId}'", 
            innerException)
    {
        StreamId = streamId;
        AggregateType = aggregateType;
    }
    
    public RehydrationException(string streamId, 
        Type aggregateType, 
        string message)
        : base($"{message}. Aggregate type: '{BasicEventTypeMap.GetTypeName(aggregateType)}'. Stream Id: '{streamId}'")
    {
        StreamId = streamId;
        AggregateType = aggregateType;
    }
    
    public RehydrationException(string streamId, 
        Type aggregateType, 
        string message,
        Exception innerException)
        : base($"{message}. Aggregate type: '{BasicEventTypeMap.GetTypeName(aggregateType)}'. Stream Id: '{streamId}'", innerException)
    {
        StreamId = streamId;
        AggregateType = aggregateType;
    }
}