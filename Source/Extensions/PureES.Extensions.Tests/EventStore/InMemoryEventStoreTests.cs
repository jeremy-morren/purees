using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Internal;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.Core.EventStore.Serialization;
using PureES.EventStore.InMemory;

namespace PureES.Extensions.Tests.EventStore;

public class InMemoryEventStoreTests : EventStoreTestsBase
{
    private readonly TestInMemoryEventStore _store;

    public InMemoryEventStoreTests() => _store = new TestInMemoryEventStore();

    protected override IEventStore CreateStore() => _store;

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    private class TestInMemoryEventStore : InMemoryEventStore
    {
        public TestInMemoryEventStore() 
            : base(TestSerializer.InMemoryEventStoreSerializer,
                new SystemClock(),
                TestSerializer.EventTypeMap)
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
        ms.Seek(0, SeekOrigin.Begin);

        var destination = new TestInMemoryEventStore();
        destination.Load(ms);

        Assert.Equal(source.ReadAll().Count, destination.ReadAll().Count);
        Assert.Equal(source.ReadAll().Select(e => new {e.StreamId, e.StreamPosition, e.EventId, e.Timestamp}),
            destination.ReadAll().Select(e => new {e.StreamId, e.StreamPosition, e.EventId, e.Timestamp}));
    }

    private static async Task Setup(IEventStore eventStore)
    {
        var data = Enumerable.Range(0, 10)
            .Select(i => $"stream-{i}")
            .SelectMany(stream => Enumerable.Range(0, 10)
                .Select(e => (stream, NewEvent())))
            .OrderBy(p => p.Item2.EventId);
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