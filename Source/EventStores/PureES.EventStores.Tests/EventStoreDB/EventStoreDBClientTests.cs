using PureES.Core.EventStore;
using PureES.EventStoreDB;

namespace PureES.EventStores.Tests.EventStoreDB;

public class EventStoreDBClientTests : EventStoreTestsBase
{
    protected override async Task<EventStoreTestHarness> CreateStore(string testName, CancellationToken ct)
    {
        var harness = await EventStoreDBTestHarness.Create(ct);

        return new EventStoreTestHarness(harness,
            new EventStoreDBClient(harness.Client, TestSerializer.EventStoreDBSerializer, new BasicEventTypeMap()));
    }
}