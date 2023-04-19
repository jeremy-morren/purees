using PureES.Core;

namespace PureES.EventBus.EventHandlers;

public class DelegateEventHandler<TEvent, TMetadata> : IEventHandler<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    private readonly IServiceProvider _services;
    private readonly Action<EventEnvelope<TEvent, TMetadata>, IServiceProvider> _delegate;

    public DelegateEventHandler(IServiceProvider services,
        Action<EventEnvelope<TEvent, TMetadata>, IServiceProvider> @delegate)
    {
        _services = services;
        _delegate = @delegate;
    }

    public Task Handle(EventEnvelope<TEvent, TMetadata> @event, CancellationToken ct)
    {
        _delegate(@event, _services);
        return Task.CompletedTask;
    }
}