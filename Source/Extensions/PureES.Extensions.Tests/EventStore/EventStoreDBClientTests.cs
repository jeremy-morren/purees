using PureES.EventStoreDB;

namespace PureES.Extensions.Tests.EventStore;

public class EventStoreDBClientTests  : EventStoreTestsBase, IClassFixture<EventStoreTestHarness>
{
    public EventStoreDBClientTests(EventStoreTestHarness harness)
        : base(() => new EventStoreDBClient(harness.GetClient(), new TestSerializer())) {}
}