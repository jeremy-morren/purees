namespace PureES.Core;

public interface IEventEnricher
{
    /// <summary>
    /// Creates metadata for an event emitted from a command
    /// </summary>
    /// <param name="command"></param>
    /// <param name="event"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    ValueTask<object?> GetMetadata(object command, object @event, CancellationToken ct);
}