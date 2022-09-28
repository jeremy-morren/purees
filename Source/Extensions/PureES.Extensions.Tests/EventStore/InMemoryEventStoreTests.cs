using System.Diagnostics.CodeAnalysis;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory;

namespace PureES.Extensions.Tests.EventStore;

public class InMemoryEventStoreTests : EventStoreTestsBase, IClassFixture<InMemoryEventStoreTests.TestInMemoryEventStore>
{
    private readonly TestInMemoryEventStore _store;

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public InMemoryEventStoreTests(TestInMemoryEventStore store) => _store = store;

    protected override IEventStore CreateStore() => _store;

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public class TestInMemoryEventStore : InMemoryEventStore
    {
        public TestInMemoryEventStore() 
            : base(new TestSerializer())
        {
        }
    }
}