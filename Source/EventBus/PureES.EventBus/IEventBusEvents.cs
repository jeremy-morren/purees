namespace PureES.EventBus;

public interface IEventBusEvents
{
    Task OnEventHandled(EventEnvelope envelope);
}