using PureES.Core.EventStore;
using PureES.EventStoreDB;

namespace PureES.EventStores.Tests.EventStoreDB;

public class EventStoreDBClientTests : EventStoreTestsBase, IClassFixture<EventStoreTestHarness>
{
    private readonly EventStoreTestHarness _harness;

    public EventStoreDBClientTests(EventStoreTestHarness harness) => _harness = harness;

    protected override IEventStore CreateStore() =>
        new EventStoreDBClient(_harness.Client, TestSerializer.EventStoreDBSerializer, TestSerializer.EventTypeMap);
}