using PureES.Core;

namespace PureES.EventBus;

public interface IEventBus
{
    Task Publish(EventEnvelope envelope, CancellationToken ct);
}
