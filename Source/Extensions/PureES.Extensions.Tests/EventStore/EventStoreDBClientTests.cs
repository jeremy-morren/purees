using System.Text.Json;
using PureES.Core.EventStore;
using PureES.EventStoreDB;
using PureES.EventStoreDB.Serialization;

namespace PureES.Extensions.Tests.EventStore;

public class EventStoreDBClientTests  : EventStoreTestsBase, IClassFixture<EventStoreTestHarness>
{
    private readonly EventStoreTestHarness _harness;

    public EventStoreDBClientTests(EventStoreTestHarness harness) => _harness = harness;

    protected override IEventStore CreateStore() => new EventStoreDBClient(_harness.GetClient(), new TestSerializer());
}