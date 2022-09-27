using System.Diagnostics.CodeAnalysis;
using PureES.EventStore.InMemory;

namespace PureES.Extensions.Tests.EventStore;

public class InMemoryEventStoreTests : EventStoreTestsBase, IClassFixture<InMemoryEventStoreTests.TestInMemoryEventStore>
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public InMemoryEventStoreTests(TestInMemoryEventStore store)
        : base(() => store) {}
    
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public class TestInMemoryEventStore : InMemoryEventStore
    {
        public TestInMemoryEventStore() 
            : base(new TestSerializer())
        {
        }
    }
}