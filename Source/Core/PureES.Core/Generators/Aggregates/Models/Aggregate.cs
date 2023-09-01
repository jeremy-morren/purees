namespace PureES.Core.Generators.Aggregates.Models;

internal record Aggregate
{
    public required IType Type { get; init; }

    /// <summary>
    /// Command handlers that create or update the aggregate
    /// </summary>
    /// <remarks>
    /// <para>If static, then CreateWhen</para>
    /// <para>Otherwise, UpdateWhen</para>
    /// </remarks>
    public required Handler[] Handlers { get; init; }

    /// <summary>
    /// Event handlers invoked for stream rehydration
    /// </summary>
    /// <remarks>
    /// <para>If static, then method is a CreateWhen</para>
    /// <para>
    /// If non-static with a generic event envelope
    /// or parameter decorated with <see cref="EventAttribute"/>,
    /// then UpdateWhen
    /// </para>
    /// <para>Otherwise, invoked for every event</para>
    /// </remarks>
    public required When[] When { get; init; }
}