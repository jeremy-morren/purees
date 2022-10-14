using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Internal;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory;

// ReSharper disable UnassignedField.Global

namespace PureES.Extensions.Benchmarks;

public class InMemoryEventStorePersistenceBenchmarks
{
    [Params(10, 100, 1000, 10_000, 100_000)]
    public int N;

    [Benchmark]
    public void SaveAndLoad()
    {
        using var ms = new MemoryStream();
        _store.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);
        new TestInMemoryEventStore().Load(ms);
    }

    private readonly InMemoryEventStore _store = new TestInMemoryEventStore();

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var @events = Enumerable.Range(0, N / 10)
            .Select(i => $"stream-{i}")
            .SelectMany(stream => Enumerable.Range(0, 10)
                .Select(_ => new UncommittedEvent(Guid.NewGuid(),
                    JsonNode.Parse("{}")!,
                    JsonNode.Parse("{}")!))
                .Select(e => (stream, e)))
            .OrderBy(p => p.e.EventId)
            .ToList();
        foreach (var (stream, e) in @events)
        {
            if (await _store.Exists(stream, default))
            {
                var revision = await _store.GetRevision(stream, default);
                await _store.Append(stream, revision, e, default);
            }
            else
            {
                await _store.Create(stream, e, default);
            }
        }
    }
    
    private class TestInMemoryEventStore : InMemoryEventStore
    {
        public TestInMemoryEventStore() 
            : base(TestSerializer.InMemoryEventStoreSerializer, new SystemClock(), TestSerializer.EventTypeMap)
        {
        }
    }
}