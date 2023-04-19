using PureES.Core;

namespace PureES.EventBus.EventHandlers;

public class AsyncDelegateEventHandler<TEvent, TMetadata> : IEventHandler<TEvent, TMetadata>
    where TEvent : notnull
    where TMetadata : notnull
{
    private readonly IServiceProvider _services;
    private readonly Func<EventEnvelope<TEvent, TMetadata>, IServiceProvider, CancellationToken, Task> _delegate;

    public AsyncDelegateEventHandler(IServiceProvider services,
        Func<EventEnvelope<TEvent, TMetadata>, IServiceProvider, CancellationToken, Task> @delegate)
    {
        _services = services;
        _delegate = @delegate;
    }

    public Task Handle(EventEnvelope<TEvent, TMetadata> @event, CancellationToken ct) => _delegate(@event, _services, ct);
}