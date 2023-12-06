namespace PureES.EventStores.Tests;

public sealed class EventStoreTestHarness : IServiceProvider, IAsyncDisposable
{
    private readonly IAsyncDisposable _harness;
    public readonly IEventStore EventStore;

    public EventStoreTestHarness(IAsyncDisposable harness, IEventStore eventStore)
    {
        _harness = harness;
        EventStore = eventStore;
    }
    
    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    public object? GetService(Type serviceType) => ((IServiceProvider)_harness).GetService(serviceType);
}