using System.Reflection;

namespace PureES.Core;

public record CommandHandlerOptions
{
    #region Get Id
    
    /// <summary>
    /// Gets the strongly-typed ID
    /// property on a command that represents
    /// the aggregate Id (with <c>StreamId</c> property)
    /// </summary>
    /// <remarks>
    /// If delegate or return type are null,
    /// framework will attempt to locate an <c>Id</c> property
    /// </remarks>
    public Func<Type, PropertyInfo?>? GetAggregateIdProperty { get; init; }
    
    /// <summary>
    /// Gets the property on an strongly-typed ID
    /// that represents the stream ID
    /// </summary>
    /// <remarks>
    /// If delegate or return type are null,
    /// framework will attempt to locate a <c>StreamId</c> property
    /// </remarks>
    public Func<Type, PropertyInfo?>? GetStreamIdProperty { get; init; }
    
    #endregion
    
    #region Event Envelope

    public Func<Type, bool>? IsEventEnvelope { get; init; }

    /// <summary>
    /// Returns the underlying Event type
    /// for a given EventEnvelope type
    /// </summary>
    public Func<Type, Type>? GetEventType { get; init; }
    
    #endregion
}