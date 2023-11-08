using System.Threading.Tasks.Dataflow;
using PureES.Core;
using Xunit;

namespace PureES.EventBus.Tests;

public class EventStreamBlockTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(10, 2)]
    [InlineData(10 * 10, 7)]
    [InlineData(100 * 100, 51)]
    public async Task Process(int count, int streamSize)
    {
        var result = new Dictionary<string, List<int>>();

        var envelopes = Enumerable.Range(0, count)
            .Select(i => new EventEnvelope($"{i / streamSize}",
                (ulong) (i % streamSize),
                DateTime.UtcNow,
                new object(),
                null))
            .ToList();

        var completions = envelopes
            .GroupBy(e => e.StreamId)
            .ToDictionary(e => e.Key, _ => new TaskCompletionSource());

        var block = new EventStreamBlock(async e =>
        {
            await completions[e.StreamId].Task;
            lock (result)
            {
                result.TryAdd(e.StreamId, new List<int>());
                result[e.StreamId].Add((int) e.StreamPosition);
            }
        }, new EventBusOptions()
        {
            MaxDegreeOfParallelism = 1,
            BufferSize = count
        });

        var all = new List<EventEnvelope>();
        var target = new ActionBlock<EventEnvelope>(all.Add);
        block.LinkTo(target, new DataflowLinkOptions()
        {
            PropagateCompletion = true
        });

        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token;

        foreach (var e in envelopes)
            await block.SendAsync(e, ct);

        //Get stream ids in random order
        var streamIds = completions.Keys
            .OrderBy(_ => Guid.NewGuid())
            .ToList(); //Enumerate it, to cache

        //Complete waits
        foreach (var id in streamIds)
            completions[id].SetResult();

        block.Complete();
        await target.Completion;

        //Ensure all streams were processed
        Assert.All(completions.Keys, s => Assert.Contains(s, result.Keys));

        //Ensure the events were processed in order
        Assert.All(result.Values, l =>
            Assert.Equal(Enumerable.Range(0, l.Count), l));
        
        //Ensure all events reached target
        Assert.All(envelopes, e => Assert.Contains(e, all));
        
        //Assert all are in stream order
        Assert.All(all.SkipLast(1).Zip(all.Skip(1)), pair =>
        {
            var (a, b) = pair;
            if (a.StreamId == b.StreamId)
                Assert.Equal(a.StreamPosition, b.StreamPosition - 1);
        });
    }
}