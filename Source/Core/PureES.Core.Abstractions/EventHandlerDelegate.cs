namespace PureES.Core;

public record EventHandlerDelegate(string Name, Func<EventEnvelope, IServiceProvider, CancellationToken, Task> Delegate);