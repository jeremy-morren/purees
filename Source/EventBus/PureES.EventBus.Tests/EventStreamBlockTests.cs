using System.Threading.Tasks.Dataflow;
using FluentAssertions;
using Xunit;

namespace PureES.EventBus.Tests;

public class EventStreamBlockTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(10, 2)]
    [InlineData(100, 51)]
    [InlineData(500, 7)]
    public async Task Process(int count, int streamSize)
    {
        foreach (var maxDegreeOfParallelism in new [] {DataflowBlockOptions.Unbounded, 1})
        {
            var result = new Dictionary<string, List<int>>();

            var envelopes = Enumerable.Range(0, count)
                .SelectMany(stream => Enumerable.Range(0, streamSize)
                    .Select(i => new EventEnvelope(stream.ToString(),
                        (uint)i,
                        DateTime.UtcNow,
                        new object(),
                        null)))
                .ToList();

            //Completion tasks for each envelope
            var completions = envelopes.ToDictionary(Key (e) => e, _ => new TaskCompletionSource());

            var ct = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
            var mutex = new SemaphoreSlim(1, 1);

            var block = new EventStreamBlock(
                async e =>
                {
                    await mutex.WaitAsync(ct);
                    try
                    {
                        await completions[e].Task;
                        result.TryAdd(e.StreamId, []);
                        result[e.StreamId].Add((int)e.StreamPosition);
                    }
                    finally
                    {
                        mutex.Release();
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    EnsureOrdered = true,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = ct
                });

            var all = new List<EventEnvelope>();
            var target = new ActionBlock<EventEnvelope>(all.Add, new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1,
                CancellationToken = ct
            });
            block.LinkTo(target, new DataflowLinkOptions()
            {
                PropagateCompletion = true
            });

            //Send in random order
            foreach (var e in Shuffle(envelopes))
                await block.SendAsync(e, ct);

            //Complete waits in random order
            foreach (var e in Shuffle(envelopes))
                completions[e].SetResult();

            block.Complete();
            await target.Completion;

            //Ensure all streams were processed
            result.Keys
                .OrderBy(int.Parse)
                .ToList()
                .Should().BeEquivalentTo(envelopes.Select(e => e.StreamId).Distinct());

            //Ensure the events were processed in order
            Assert.All(result, pair =>
                pair.Value.Should()
                    .BeInAscendingOrder().And
                    .StartWith(0).And
                    .HaveCount(streamSize));

            // Ensure all events reached target
            all.OrderBy(e => int.Parse(e.StreamId))
                .ThenBy(e => e.StreamPosition)
                .ToList()
                .Should().BeEquivalentTo(envelopes);
        }
    }

    private readonly record struct Key(string StreamId, uint StreamPosition)
    {
        public Key(IEventEnvelope env) : this(env.StreamId, env.StreamPosition)
        {
        }

        public static implicit operator Key(EventEnvelope env) => new(env);
    }

    /// <summary>
    /// Shuffle the envelopes (but keep the order within each stream)
    /// </summary>
    private static IEnumerable<EventEnvelope> Shuffle(IEnumerable<EventEnvelope> envelopes)
    {
        var list = envelopes.ToList();
        while (list.Count > 0)
        {
            //Get the first envelope in each stream
            //Get a random one, remove and return
            var env = list
                .GroupBy(e => e.StreamId)
                .Select(g => g.First())
                .OrderBy(_ => Guid.NewGuid())
                .First();
            list.Remove(env);
            yield return env;
        }
    }
}