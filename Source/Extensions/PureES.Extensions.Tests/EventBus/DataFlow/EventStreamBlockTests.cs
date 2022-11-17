using System.Threading.Tasks.Dataflow;
using PureES.Core;
using PureES.EventBus.DataFlow;

namespace PureES.Extensions.Tests.EventBus.DataFlow;

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
            .Select(i => new EventEnvelope(Guid.NewGuid(),
                $"{i / streamSize}",
                (ulong) (i % streamSize),
                (ulong)i,
                DateTime.Now,
                new object(),
                null))
            .ToList();

        var completions = envelopes
            .GroupBy(e => e.StreamId)
            .ToDictionary(e => e.Key, _ => new TaskCompletionSource());

        var block = new EventStreamBlock(async (e, _) =>
        {
            await completions[e.StreamId].Task;
            lock (result)
            {
                result.TryAdd(e.StreamId, new List<int>());
                result[e.StreamId].Add((int)e.StreamPosition);
            }
        }, new EventStreamBlockOptions()
        {
            MaxDegreeOfParallelism = -1
        });
        
        foreach (var e in envelopes)
            block.Post(e);

        var streamIds = completions.Keys
            .OrderBy(_ => Guid.NewGuid())
            .ToList(); //Enumerate it
        
        //Complete waits in random order
        foreach (var id in streamIds)
            completions[id].SetResult();
        
        block.Complete();
        await block.Completion;
        
        //Ensure all streams were processed
        Assert.All(completions.Keys, s => Assert.Contains(s, result.Keys));

        //Ensure the events were processed in order
        Assert.All(result.Values, l => 
            Assert.Equal(Enumerable.Range(0, l.Count), l));
    }
}