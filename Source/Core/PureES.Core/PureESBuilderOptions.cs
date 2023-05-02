

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PureES.Core;

public record PureESBuilderOptions
{
    #region Get Id

    /// <summary>
    ///     Gets the strongly-typed ID
    ///     property on a command that represents
    ///     the aggregate Id
    /// </summary>
    /// <remarks>
    ///     If delegate or return type are null,
    ///     framework will attempt to locate an <c>Id</c> property
    /// </remarks>
    public Func<Type, PropertyInfo?>? GetAggregateIdProperty { get; init; }

    /// <summary>
    ///     Gets the property on an strongly-typed ID
    ///     that represents the stream ID
    /// </summary>
    /// <remarks>
    ///     If delegate or return type are null,
    ///     framework will attempt to locate a <c>StreamId</c> property
    /// </remarks>
    public Func<Type, PropertyInfo?>? GetStreamIdProperty { get; init; }

    #endregion

    #region Event Envelope

    /// <summary>
    /// A delegate that indicates whether a parameter is a strongly typed event envelope parameter
    /// </summary>
    public Func<Type, bool>? IsStronglyTypedEventEnvelope { get; init; }

    /// <summary>
    ///     Returns the underlying Event type
    ///     for a given EventEnvelope type
    /// </summary>
    public Func<Type, Type>? GetEventType { get; init; }

    #endregion
}