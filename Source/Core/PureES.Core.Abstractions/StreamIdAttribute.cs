namespace PureES.Core;

/// <summary>
/// Indicates that a Command/Event always belongs to the same stream
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.All)]
public sealed class StreamIdAttribute : Attribute
{
    /// <summary>
    /// The ID of the stream
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// Indicates that a Command/Event always belongs to the same stream
    /// </summary>
    /// <param name="streamId">The ID of the stream</param>
    public StreamIdAttribute(string streamId) => StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
}