namespace PureES.Core;

/// <summary>
/// Indicates that a Command/Event always belongs to the same stream
/// (specified in the <see cref="StreamId"/> property
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.All)]
public sealed class StreamIdAttribute : Attribute
{
    public string StreamId { get; }

    public StreamIdAttribute(string streamId) => StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
}