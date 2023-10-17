using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Moq;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory;
using PureES.EventStore.InMemory.Serialization;

namespace PureES.EventStores.Tests;

public class InMemoryEventStoreTests : EventStoreTestsBase
{
    protected override Task<EventStoreTestHarness> CreateStore(string testName, CancellationToken ct) =>
        Task.FromResult(new EventStoreTestHarness(new Mock<IAsyncDisposable>().Object, new TestInMemoryEventStore()));

    [Fact]
    public void SaveAndLoad()
    {
        var source = new TestInMemoryEventStore();
        Setup(source).GetAwaiter().GetResult();

        AssertEqual(source, source);

        using var ms = new MemoryStream();
        source.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);

        var destination = new TestInMemoryEventStore();
        destination.Load(ms);

        AssertEqual(source, destination);
        AssertEqual(destination, destination);
    }

    private static void AssertEqual(IInMemoryEventStore left, IInMemoryEventStore right)
    {
        Assert.Equal(left.ReadAll().Count, right.ReadAll().Count);
        Assert.Equal(left.ReadAll().Select(e => new {e.StreamId, e.StreamPosition, e.EventId, e.Timestamp}),
            right.ReadAll().Select(e => new {e.StreamId, e.StreamPosition, e.EventId, e.Timestamp}));

        Assert.All(left.ReadAll().Concat(right.ReadAll()), 
            e => Assert.Equal(DateTimeKind.Utc, e.Timestamp.Kind));
    }

    private static async Task Setup(IEventStore eventStore)
    {
        var data = Enumerable.Range(0, 10)
            .Select(i => $"stream-{i}")
            .SelectMany(stream => Enumerable.Range(0, 10)
                .Select(e => (stream, NewEvent())))
            .OrderBy(p => p.Item2.EventId);
        foreach (var (stream, e) in data)
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

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    private sealed class TestInMemoryEventStore : InMemoryEventStore
    {
        public TestInMemoryEventStore()
            : base(Serializer,
                new SystemClock(),
                new BasicEventTypeMap())
        {
        }
        
        
        public static InMemoryEventStoreSerializer Serializer =>
            new (new BasicEventTypeMap(), new OptionsWrapper<InMemoryEventStoreOptions>(new InMemoryEventStoreOptions()));
    }
}