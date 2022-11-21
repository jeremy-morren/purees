using PureES.Core;

namespace PureES.EventBus;

public interface IEventHandler<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    Task Handle(EventEnvelope<TEvent, TMetadata> @event, CancellationToken ct);
}