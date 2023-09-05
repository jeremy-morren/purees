using System.Diagnostics;
using System.Runtime.CompilerServices;
using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStores.Tests;

public abstract class EventStoreTestsBase
{
    protected abstract Task<EventStoreTestHarness> CreateStore(string testName, CancellationToken ct);

    [DebuggerNonUserCode]
    protected Task<EventStoreTestHarness> GetStore([CallerMemberName] string testName = null!)
    {
        if (testName == null) throw new ArgumentNullException(nameof(testName));
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
        return CreateStore($"{testName}-{Environment.Version}", ct);
    }

    [DebuggerNonUserCode]
    protected static CancellationToken CancellationToken => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
    

    [Fact]
    public async Task Create()
    {
        await using var store = await GetStore();
        const string stream = nameof(Create);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.False(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        Assert.True(await store.Exists(stream, CancellationToken));
        
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }
    
    [Fact]
    public async Task CreateSingle()
    {
        await using var store = await GetStore();
        const string stream = nameof(CreateSingle);
        var @event = NewEvent();
        const ulong revision = 0;
        Assert.False(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.Create(stream, @event, CancellationToken));
        Assert.True(await store.Exists(stream, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        await AssertEqual(new [] { @event }, d => store.Read(d, stream, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
    }

    [Fact]
    public async Task Create_Existing_Should_Throw()
    {
        await using var store = await GetStore();
        const string stream = nameof(Create_Existing_Should_Throw);
        const ulong revision = 0;
        Assert.Equal(revision, await store.Create(stream, NewEvent(), CancellationToken));
        var ex = await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.Create(stream, NewEvent(), CancellationToken));
        Assert.Equal(revision, ex.CurrentRevision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append(bool useOptimisticConcurrency)
    {
        await using var store = await GetStore($"{nameof(Append)}+{useOptimisticConcurrency}");
        const string stream = nameof(Append);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        Assert.Equal((ulong) 4, await store.Create(stream, events.Take(5), CancellationToken));
        Assert.Equal((ulong) 9, useOptimisticConcurrency
            ? await store.Append(stream, 4, events.Skip(5), CancellationToken)
            : await store.Append(stream, events.Skip(5), CancellationToken));
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        Assert.Equal((ulong) events.Count - 1, await store.GetRevision(stream, CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append_To_Invalid_Should_Throw(bool useOptimisticConcurrency)
    {
        await using var store = await GetStore($"{nameof(Append_To_Invalid_Should_Throw)}+{useOptimisticConcurrency}");
        const string stream = nameof(Append_To_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            useOptimisticConcurrency
                ? store.Append(stream, 0, NewEvent(), CancellationToken)
                : store.Append(stream, NewEvent(), CancellationToken));
    }

    [Fact]
    public async Task Append_With_Invalid_Revision_Should_Throw()
    {
        await using var store = await GetStore();
        const string stream = nameof(Append_With_Invalid_Revision_Should_Throw);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.Append(stream, RandVersion(events.Count + 1), NewEvent(), CancellationToken));
        Assert.Equal(revision, ex.ActualRevision);
    }

    [Fact]
    public async Task Read_Invalid_Stream_Should_Throw()
    {
        await using var store = await GetStore();
        const string stream = nameof(Read_Invalid_Stream_Should_Throw);
        Assert.False(await store.Exists(stream, CancellationToken));
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, CancellationToken));
        
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () => 
            await store.Read(Direction.Forwards, stream, CancellationToken).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () => 
            await store.Read(Direction.Backwards, stream, CancellationToken).FirstAsync());
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.Read(Direction.Forwards, stream, RandVersion(), CancellationToken).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.Read(Direction.Backwards, stream, RandVersion(), CancellationToken).FirstAsync());
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadPartial(Direction.Forwards, stream, RandVersion(), CancellationToken).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadPartial(Direction.Backwards, stream, RandVersion(), CancellationToken).FirstAsync());
    }

    [Fact]
    public async Task Read()
    {
        await using var store = await GetStore();
        const string stream = nameof(Read);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));

        await AssertEqual(events, d => store.Read(d, stream, revision, CancellationToken));
        
        await AssertEqual(events.Take(5), store.ReadPartial(Direction.Forwards, stream, 4, CancellationToken));
        
        await AssertEqual(events.TakeLast(5).Reverse(), store.ReadPartial(Direction.Backwards, stream, 4, CancellationToken));

        async Task AssertWrongVersion(Func<Direction, IAsyncEnumerable<EventEnvelope>> getEvents)
        {
            var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () =>
                await getEvents(Direction.Forwards).CountAsync());
            Assert.Equal(revision, ex.ActualRevision);
            
            ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () => 
                await getEvents(Direction.Backwards).CountAsync());
            
            Assert.Equal(revision, ex.ActualRevision);
        }

        await AssertWrongVersion(d => 
            store.Read(d, stream, RandVersion(events.Count + 1), CancellationToken));

        await AssertWrongVersion(d => 
            store.Read(d, stream, 0, CancellationToken));

        await AssertWrongVersion(d => 
            store.Read(d, stream, RandVersion(events.Count + 1), CancellationToken));
    }

    [Fact]
    public async Task GetStreamRevision()
    {
        await using var store = await GetStore();
        const string stream = nameof(GetStreamRevision);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        await store.Create(stream, events, CancellationToken);
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, revision, CancellationToken));
        await Assert.ThrowsAsync<WrongStreamRevisionException>(() => store.GetRevision(stream, revision + 1, CancellationToken));
    }

    [Fact]
    public async Task Get_Revision_Invalid_Should_Throw()
    {
        await using var store = await GetStore();
        const string stream = nameof(Get_Revision_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, CancellationToken));
    }

    [Fact]
    public async Task ReadAll()
    {
        await using var store = await GetStore();
        const string stream = nameof(ReadAll);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, CancellationToken));
        Assert.Equal(revision, await store.GetRevision(stream, CancellationToken));

        var forwards = await store.ReadAll(Direction.Forwards, CancellationToken).ToListAsync();
        Assert.NotEmpty(forwards);

        var sorted = forwards
            .OrderBy(e => e.Timestamp)
            .ThenBy(a => a.StreamId)
            .ThenBy(a => a.StreamPosition)
            .Select(a => $"{a.StreamId}/{a.StreamPosition}")
            .ToArray();
        
        Assert.Equal(sorted,
            forwards.Select(a => $"{a.StreamId}/{a.StreamPosition}").ToArray());
        
        Assert.Empty(forwards.GroupBy(a => new {a.StreamId, a.StreamPosition})
            .Where(g => g.Count() > 1));

        forwards.Reverse();
        var backwards = await store.ReadAll(Direction.Backwards, CancellationToken).ToListAsync();

        Assert.Equal(forwards.Select(e => e.StreamId), backwards.Select(s => s.StreamId));

        Assert.Single(await store.ReadAll(Direction.Forwards, 1, CancellationToken).ToListAsync());
        Assert.Single(await store.ReadAll(Direction.Backwards, 1, CancellationToken).ToListAsync());
    }

    [Fact]
    public async Task StreamPosition_Should_Be_Ordered_And_Unique()
    {
        await using var store = await GetStore();
        const string stream = nameof(StreamPosition_Should_Be_Ordered_And_Unique);
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        await store.Create(stream, events.Take(count / 2), CancellationToken);
        await store.Append(stream, events.Skip(count / 2), CancellationToken);

        Assert.Equal((ulong) count - 1, await store.GetRevision(stream, CancellationToken));
        
        var list = await store.Read(Direction.Forwards, stream, CancellationToken).ToListAsync();
        
        Assert.Equal(list.OrderBy(l => l.StreamPosition)
            .Select(l => l.StreamPosition),
            list.Select(l => l.StreamPosition));
        Assert.Equal(Enumerable.Range(0, count), list.Select(e => (int) e.StreamPosition));

        Assert.Equal(list.OrderBy(e => e.Timestamp), list);

        list = await store.Read(Direction.Backwards, stream, CancellationToken).ToListAsync();
        
        Assert.Equal(list.OrderByDescending(l => l.StreamPosition)
                .Select(l => l.StreamPosition),
            list.Select(l => l.StreamPosition));
        Assert.Equal(Enumerable.Range(0, count).Reverse(), list.Select(e => (int) e.StreamPosition));

        Assert.Equal(list.OrderByDescending(e => e.Timestamp), list);
    }

    [Fact]
    public async Task ReadByEventType()
    {
        await using var store = await GetStore();
        
        const string stream = nameof(ReadByEventType);
        var events = Enumerable.Range(0, 100).Select(_ => NewEvent()).ToList();
        await store.Create(stream, events, CancellationToken);

        Assert.NotEmpty(await store.ReadByEventType(Direction.Forwards, typeof(Event), CancellationToken).ToListAsync());
        Assert.NotEmpty(await store.ReadByEventType(Direction.Backwards, typeof(Event), CancellationToken).ToListAsync());

        Assert.Single(
            await store.ReadByEventType(Direction.Forwards,typeof(Event), 1, CancellationToken).ToListAsync());
        Assert.Single(
            await store.ReadByEventType(Direction.Backwards,typeof(Event), 1, CancellationToken).ToListAsync());
    }
    
    [Fact]
    public async Task Count()
    {
        await using var store = await GetStore();

        const int count = 10;
        
        foreach (var stream in Enumerable.Range(0, count))
            await store.Create($"stream-{stream}",
                Enumerable.Range(0, count).Select(_ => NewEvent()),
                CancellationToken);
        
        Assert.Equal((ulong)count * count, await store.Count(CancellationToken));
    }
    
    [Fact]
    public async Task CountByEventType()
    {
        await using var store = await GetStore();

        const string stream = nameof(CountByEventType);
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        await store.Create(stream, events, CancellationToken);

        Assert.Equal((ulong)count, await store.CountByEventType(typeof(Event), CancellationToken));
    }

    [Fact]
    public async Task Read_Multiple_Should_Return_In_Input_Order()
    {
        await using var store = await GetStore();
        
        //We need to add in random order
        var streamIds = Enumerable.Range(0, 10)
            .SelectMany(i => Enumerable.Range(0, 10).Select(_ => $"stream-{i}"))
            .OrderBy(_ => Guid.NewGuid());

        foreach (var streamId in streamIds)
        {
            try
            {
                await store.Append(streamId, NewEvent(), CancellationToken);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId, NewEvent(), CancellationToken);
            }
            //Append bogus streams to test filter
            try
            {
                await store.Append(streamId + "-other", NewEvent(), CancellationToken);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId + "-other", NewEvent(), CancellationToken);
            }
        }

        var half = Enumerable.Range(0, 10)
            .Select(i => $"stream-{i}")
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToList();
        
        var syncResult = await store.ReadMultiple(Direction.Forwards, half, CancellationToken)
            .SelectAwait(s => s.ToListAsync())
            .ToListAsync();
        var asyncResult = await store.ReadMultiple(Direction.Forwards, half.ToAsyncEnumerable(), CancellationToken)
            .SelectAwait(s => s.ToListAsync())
            .ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            Assert.NotEmpty(result);
            Assert.All(result, stream =>
            {
                Assert.NotEmpty(stream);
                Assert.Equal(stream, stream.OrderBy(r => r.Timestamp));

                Assert.Contains(stream[0].StreamId, half);
            });
        });
        
        syncResult = await store.ReadMultiple(Direction.Backwards, half, CancellationToken)
            .SelectAwait(s => s.ToListAsync())
            .ToListAsync();
        asyncResult = await store.ReadMultiple(Direction.Backwards, half.ToAsyncEnumerable(), CancellationToken)
            .SelectAwait(s => s.ToListAsync())
            .ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            Assert.NotEmpty(result);
            Assert.All(result, stream =>
            {
                Assert.NotEmpty(stream);
                Assert.Equal(stream, stream.OrderByDescending(r => r.Timestamp));
                
                Assert.Contains(stream[0].StreamId, half);
            });
        });
    }
    
    [Fact]
    public async Task Read_Many_Should_Return_In_Chronological_Order()
    {
        await using var store = await GetStore();
        
        //We need to add in random order
        var streamIds = Enumerable.Range(0, 10)
            .SelectMany(i => Enumerable.Range(0, 10).Select(_ => $"stream-{i}"))
            .OrderBy(_ => Guid.NewGuid());

        foreach (var streamId in streamIds)
        {
            try
            {
                await store.Append(streamId, NewEvent(), CancellationToken);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId, NewEvent(), CancellationToken);
            }
            //Append bogus streams to test filter
            try
            {
                await store.Append(streamId + "-other", NewEvent(), CancellationToken);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId + "-other", NewEvent(), CancellationToken);
            }
        }

        var half = Enumerable.Range(0, 10)
            .Select(i => $"stream-{i}")
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToHashSet();
        
        var syncResult = await store.ReadMany(Direction.Forwards, half, CancellationToken).ToListAsync();
        var asyncResult = await store.ReadMany(Direction.Forwards, half.ToAsyncEnumerable(), CancellationToken).ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            Assert.NotEmpty(result);
            Assert.Equal(5 * 10, result.Count);
            Assert.Equal(result, result.OrderBy(r => r.Timestamp));

            Assert.Empty(result.Where(e => !half.Contains(e.StreamId)));
        });
        
        syncResult = await store.ReadMany(Direction.Backwards, half, CancellationToken).ToListAsync();
        asyncResult = await store.ReadMany(Direction.Backwards, half.ToAsyncEnumerable(), CancellationToken).ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            Assert.NotEmpty(result);
            Assert.Equal(5 * 10, result.Count);
            Assert.Equal(result, result.OrderByDescending(r => r.Timestamp));
            
            Assert.Empty(result.Where(e => !half.Contains(e.StreamId)));
        });
    }

    protected static async Task AssertEqual(IEnumerable<UncommittedEvent> source, Func<Direction, IAsyncEnumerable<EventEnvelope>> readEvents)
    {
        var list = source.ToList();
        await AssertEqual(list, readEvents(Direction.Forwards));
        
        list.Reverse();
        await AssertEqual(list, readEvents(Direction.Backwards));
    }
    
    protected static async Task AssertEqual(IEnumerable<UncommittedEvent> source, IAsyncEnumerable<EventEnvelope> events)
    {
        Assert.Equal(source.Select(e => e.EventId),
            await events.Select(e => e.EventId).ToListAsync());
    }

    private static ulong RandVersion(long? min = null) => (ulong) Random.Shared.NextInt64(min ?? 0, int.MaxValue - 1);

    protected static UncommittedEvent NewEvent()
    {
        var id = Guid.NewGuid();
        return new UncommittedEvent()
        {
            EventId = id,
            Event = new Event(id),
            Metadata = new Metadata()
        };
    }

    public record Event(Guid Id);

    protected record Metadata;
}