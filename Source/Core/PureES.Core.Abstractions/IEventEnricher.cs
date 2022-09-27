namespace PureES.Core;

public interface IEventEnricher
{
    ValueTask<object?> GetMetadata(object command, object @event, CancellationToken ct);
}