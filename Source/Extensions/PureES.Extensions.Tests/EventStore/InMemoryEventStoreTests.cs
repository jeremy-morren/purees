using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Internal;
using PureES.Core;
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
            : base(new TestSerializer(), new SystemClock())
        {
        }
    }

    [Fact]
    public void SaveAndLoad()
    {
        var source = new TestInMemoryEventStore();
        Setup(source).GetAwaiter().GetResult();
        using var ms = new MemoryStream();
        source.Save(ms);

        ms.Position = 0;
        
        var destination = new TestInMemoryEventStore();
        destination.Load(ms);

        Assert.Equal(source.GetAll().Count, destination.GetAll().Count);
        Assert.Equal(source.GetAll().Select(e => new {e.StreamId, e.StreamPosition, e.EventId, e.Timestamp}),
            destination.GetAll().Select(e => new {e.StreamId, e.StreamPosition, e.EventId, e.Timestamp}));
    }
    
    [Fact]
    public async Task SaveAndLoadAsync()
    {
        var source = new TestInMemoryEventStore();
        await Setup(source);
        using var ms = new MemoryStream();
        await source.SaveAsync(ms);

        ms.Position = 0;
        
        var destination = new TestInMemoryEventStore();
        await destination.LoadAsync(ms);

        Assert.Equal(source.GetAll().Count, destination.GetAll().Count);
        Assert.Equal(source.GetAll().Select(e => new {e.StreamId, e.StreamPosition, e.EventId, e.Timestamp}),
            destination.GetAll().Select(e => new {e.StreamId, e.StreamPosition, e.EventId, e.Timestamp}));
    }
    
    private static async Task Setup(IEventStore eventStore)
    {
        var data = Enumerable.Range(0, 10)
            .Select(i => $"stream-{i}")
            .SelectMany(stream => Enumerable.Range(0, 10)
                .Select(_ => new UncommittedEvent(Guid.NewGuid(), 
                    JsonNode.Parse("{}")!, 
                    JsonNode.Parse("{}")!))
                .Select(e => (stream, e)))
            .OrderBy(p => p.e.EventId);
        foreach (var (stream, e) in data)
        {
            if (await eventStore.Exists(stream, default))
            {
                var revision = await eventStore.GetRevision(stream, default);
                await eventStore.Append(stream, revision, e, default);
            }
            else
            {
                await eventStore.Create(stream, e, default);
            }
        }
    }
}