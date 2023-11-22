using System.Diagnostics;
using System.Runtime.CompilerServices;
using FluentAssertions;
using JasperFx.Core;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core;
using Shouldly;

namespace PureES.EventStores.Tests;

public abstract class EventStoreTestsBase
{
    protected abstract Task<EventStoreTestHarness> CreateStore(string testName, 
        Action<IServiceCollection> configureServices,
        CancellationToken ct);

    [DebuggerNonUserCode]
    protected Task<EventStoreTestHarness> CreateHarness([CallerMemberName] string testName = null!) =>
        CreateHarness(_ => {}, testName);
    
    protected Task<EventStoreTestHarness> CreateHarness(Action<IServiceCollection> configureServices,
        [CallerMemberName] string testName = null!)
    {
        if (testName == null) throw new ArgumentNullException(nameof(testName));
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
        return CreateStore($"{testName}-{Environment.Version}", configureServices, ct);
    }

    [DebuggerNonUserCode]
    protected static CancellationToken CancellationToken => new CancellationTokenSource(TimeSpan.FromMinutes(10)).Token;
    
    [Fact]
    public async Task Create()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        (await store.Exists(stream, CancellationToken)).ShouldBeFalse();

        (await store.Create(stream, events, CancellationToken)).ShouldBe(revision);
        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);
        (await store.Exists(stream, CancellationToken)).ShouldBeTrue();
        
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));

        (await store.GetRevision(stream, CancellationToken)).ShouldBe((ulong)events.Count - 1);
    }
    
    [Fact]
    public async Task Create_Single()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create_Single);
        var @event = NewEvent();
        const ulong revision = 0;
        (await store.Exists(stream, CancellationToken)).ShouldBeFalse();
        (await store.Create(stream, @event, CancellationToken)).ShouldBe(revision);

        (await store.Exists(stream, CancellationToken)).ShouldBeTrue();
        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);
        
        await AssertEqual(new [] { @event }, d => store.Read(d, stream, CancellationToken));
        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);
    }

    [Fact]
    public async Task Submit_Transaction()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        var transaction = new EventsTransaction();
        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken);
        
        transaction.Clear();
        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), (ulong)i, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(5, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));

        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken);

        var totalCount = 0;
        foreach (var i in Enumerable.Range(0, 5))
        {
            var count = (i + 1) * 2;
            (await store.GetRevision(i.ToString(), CancellationToken)).ShouldBe((ulong)count - 1);
            (await store.Read(Direction.Forwards, i.ToString(), CancellationToken).ToListAsync())
                .Should().HaveCount(count);
            totalCount += count;
        }

        foreach (var i in Enumerable.Range(5, 5))
        {
            var count = i + 1;
            (await store.GetRevision(i.ToString(), CancellationToken)).ShouldBe((ulong)count - 1);
            (await store.Read(Direction.Forwards, i.ToString(), CancellationToken).ToListAsync())
                .Should().HaveCount(count);
            totalCount += count;
        }
        
        (await store.ReadAll(Direction.Forwards, CancellationToken).ToListAsync()).Should().HaveCount(totalCount);
        (await store.Count(CancellationToken)).ShouldBe((ulong)totalCount);
        
        transaction.Clear();
        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), (ulong)i, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(5, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(10, 5))
            transaction.Add(i.ToString(), (ulong)i + 1, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        foreach (var i in Enumerable.Range(15, 5)) //Valid, test atomicity
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        var ex = await Assert.ThrowsAsync<EventsTransactionException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken));

        ex.InnerExceptions.Should().HaveCount(15);
        ex.InnerExceptions.OfType<WrongStreamRevisionException>().Should().HaveCount(5);
        ex.InnerExceptions.OfType<StreamAlreadyExistsException>().Should().HaveCount(5);
        ex.InnerExceptions.OfType<StreamNotFoundException>().Should().HaveCount(5);
        
        //Ensure the valid ones weren't committed
        Assert.All(Enumerable.Range(15, 5), i =>
        {
            store.Exists(i.ToString(), CancellationToken).Result.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Create_Single_Existing_Should_Throw()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create_Existing_Should_Throw);
        const ulong revision = 0;
        (await store.Create(stream, NewEvent(), CancellationToken)).ShouldBe(revision);
        
        await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.Create(stream, NewEvent(), CancellationToken));
    }
    
    [Fact]
    public async Task Create_Existing_Should_Throw()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();

        (await store.Create(stream, events, CancellationToken)).ShouldBe((ulong) events.Count - 1);
        
        var ex = await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.Create(stream, events, CancellationToken));
        ex.StreamId.ShouldBe(stream);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append(bool useOptimisticConcurrency)
    {
        await using var harness = await CreateHarness($"{nameof(Append)}+{useOptimisticConcurrency}");
        var store = harness.EventStore;
        const string stream = nameof(Append);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        (await store.Create(stream, events.Take(5), CancellationToken)).ShouldBe(4ul);

        if (useOptimisticConcurrency)
            (await store.Append(stream, 4, events.Skip(5), CancellationToken)).ShouldBe(9ul);
        else
            (await store.Append(stream, events.Skip(5), CancellationToken)).ShouldBe(9ul);
        
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));
        (await store.GetRevision(stream, CancellationToken)).ShouldBe((ulong)events.Count - 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append_To_Invalid_Should_Throw(bool useOptimisticConcurrency)
    {
        await using var harness = await CreateHarness($"{nameof(Append_To_Invalid_Should_Throw)}+{useOptimisticConcurrency}");
        var store = harness.EventStore;
        const string stream = nameof(Append_To_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            useOptimisticConcurrency
                ? store.Append(stream, 0, NewEvent(), CancellationToken)
                : store.Append(stream, NewEvent(), CancellationToken));
        
        await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            useOptimisticConcurrency
                ? store.Append(stream, 0, new [] { NewEvent() }, CancellationToken)
                : store.Append(stream, new [] { NewEvent() }, CancellationToken));
    }

    [Fact]
    public async Task Append_With_Invalid_Revision_Should_Throw()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Append_With_Invalid_Revision_Should_Throw);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        (await store.Create(stream, events, CancellationToken)).ShouldBe(revision);
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.Append(stream, RandVersion(events.Count + 1), events, CancellationToken));
        ex.ActualRevision.ShouldBe(revision);
    }
    
    [Fact]
    public async Task Append_Single_With_Invalid_Revision_Should_Throw()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Append_With_Invalid_Revision_Should_Throw);
        var @event = NewEvent();
        (await store.Create(stream, @event, CancellationToken)).ShouldBe(0ul);
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.Append(stream, 1, NewEvent(), CancellationToken));
        ex.ActualRevision.ShouldBe(0ul);
    }

    [Fact]
    public async Task Read_Invalid_Stream_Should_Throw()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
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
            await store.Read(Direction.Backwards, stream, RandVersion() % (int)short.MaxValue, RandVersion(short.MaxValue), CancellationToken).FirstAsync());
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadPartial(Direction.Forwards, stream, RandVersion(), CancellationToken).FirstAsync());
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadPartial(Direction.Backwards, stream, RandVersion(), CancellationToken).FirstAsync());
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadSlice(stream, RandVersion() % (int)short.MaxValue, CancellationToken).FirstAsync());
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadSlice(stream, RandVersion() % (int)short.MaxValue, RandVersion(short.MaxValue), CancellationToken).FirstAsync());
    }

    [Fact]
    public async Task Read()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Read);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (ulong) events.Count - 1;
        (await store.Create(stream, events, CancellationToken)).ShouldBe(revision);
        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);

        await AssertEqual(events, d => store.Read(d, stream, revision, CancellationToken));
        
        await AssertEqual(events.Skip(2), d => store.Read(d, stream, 2, revision, CancellationToken));
        
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            store.Read(Direction.Forwards, stream, 3, 2, CancellationToken)
                .ToListAsync().AsTask());
        
        await AssertEqual(events.Take(5), store.ReadPartial(Direction.Forwards, stream, 5, CancellationToken));
        
        await AssertEqual(events.TakeLast(3).Reverse(), store.ReadPartial(Direction.Backwards, stream, 3, CancellationToken));
        
        await AssertEqual(events, store.ReadSlice(stream, 0, CancellationToken));
        
        await AssertEqual(events.Skip(5), store.ReadSlice(stream, 5, CancellationToken));
        
        await AssertEqual(events.Take(3), store.ReadSlice(stream, 0, 2, CancellationToken));
        
        await AssertEqual(events.Skip(4).Take(2), store.ReadSlice(stream, 4, 5, CancellationToken));
        
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            store.ReadSlice(stream, 3, 2, CancellationToken)
                .ToListAsync().AsTask());

        async Task AssertWrongVersion(Func<Direction, IAsyncEnumerable<EventEnvelope>> getEvents)
        {
            var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () =>
                await getEvents(Direction.Forwards).CountAsync());
            ex.ActualRevision.ShouldBe(revision);
            
            ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () => 
                await getEvents(Direction.Backwards).CountAsync());

            ex.ActualRevision.ShouldBe(revision);
        }

        await AssertWrongVersion(d => 
            store.Read(d, stream, RandVersion(events.Count + 1), CancellationToken));

        await AssertWrongVersion(d => 
            store.Read(d, stream, 0, CancellationToken));

        await AssertWrongVersion(d => 
            store.Read(d, stream, RandVersion(events.Count + 1), CancellationToken));
        
        await AssertWrongVersion(d => 
            store.Read(d, stream, 2, RandVersion(events.Count + 1), CancellationToken));
        
        await AssertWrongVersion(d => 
            store.ReadPartial(d, stream, RandVersion(events.Count + 1), CancellationToken));
        
        await AssertWrongVersion(_ => 
            store.ReadSlice(stream, 1, RandVersion(events.Count + 1), CancellationToken));
        
        await AssertWrongVersion(_ => 
            store.ReadSlice(stream, RandVersion(events.Count + 1), ulong.MaxValue, CancellationToken));
        
        await AssertWrongVersion(_ => 
            store.ReadSlice(stream, RandVersion(events.Count + 1), CancellationToken));
    }
    
    [Fact]
    public async Task ReadPartial_With_Count_Zero_Should_Throw()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            store.ReadPartial(Direction.Forwards, "stream", 0, CancellationToken).ToListAsync().AsTask());
        ex.ParamName.ShouldBe("count");
    }

    [Fact]
    public async Task GetStreamRevision()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
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
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Get_Revision_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, CancellationToken));
    }

    [Fact]
    public async Task ReadAll()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
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
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(StreamPosition_Should_Be_Ordered_And_Unique);
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        
        (await store.Create(stream, events.Take(count / 2), CancellationToken)).ShouldBe((ulong)count / 2 - 1);
        (await store.Append(stream, events.Skip(count / 2), CancellationToken)).ShouldBe((ulong)count - 1);

        (await store.GetRevision(stream, CancellationToken)).ShouldBe((ulong)count - 1);
        
        var list = await store.Read(Direction.Forwards, stream, CancellationToken).ToListAsync();

        list[0].StreamPosition.ShouldBe(0ul);
        list[^1].StreamPosition.ShouldBe((ulong)count - 1);
        list.Should().BeInAscendingOrder(s => s.StreamPosition);
        list.Should().BeInAscendingOrder(s => s.Timestamp);
        
        list.Select(e => e.StreamPosition).Should()
            .BeEquivalentTo(Enumerable.Range(0, count));
        
        list = await store.Read(Direction.Backwards, stream, CancellationToken).ToListAsync();

        list[0].StreamPosition.ShouldBe((ulong)count - 1);
        list[^1].StreamPosition.ShouldBe(0ul);
        list.Should().BeInDescendingOrder(l => l.StreamPosition);
        list.Should().BeInDescendingOrder(e => e.Timestamp);
        
        list.Select(e => e.StreamPosition).Should()
            .BeEquivalentTo(Enumerable.Range(0, count).Reverse());

    }

    [Fact]
    public async Task ReadByEventType()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        
        const int count = 10;
        for (var i = 0; i < count; i++)
        {
            var events = Enumerable.Range(0, count).Select(_ => NewEvent());
            await store.Create($"{i}-{nameof(ReadByEventType)}", events, CancellationToken);
        }

        var forwards =await store.ReadByEventType(Direction.Forwards, typeof(Event), CancellationToken).ToListAsync();
            
        forwards.ShouldNotBeEmpty();
        forwards.Should().HaveCount(count * count);
        forwards.Should().BeInAscendingOrder(e => e.Timestamp);
        forwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);
        
        var backwards = await store.ReadByEventType(Direction.Backwards, typeof(Event), CancellationToken).ToListAsync();

        backwards.ShouldNotBeEmpty();
        backwards.Should().HaveCount(count * count);
        backwards.Should().BeInDescendingOrder(e => e.Timestamp);
        backwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);

        (await store.ReadByEventType(Direction.Forwards, typeof(Event), 1, CancellationToken).ToListAsync())
            .ShouldHaveSingleItem();
        
        (await store.ReadByEventType(Direction.Backwards, typeof(Event), 1, CancellationToken).ToListAsync())
            .ShouldHaveSingleItem();
    }
    
    [Fact]
    public async Task Count()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

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
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        const string stream = nameof(CountByEventType);
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        await store.Create(stream, events, CancellationToken);

        (await store.CountByEventType(typeof(Event), CancellationToken)).ShouldBe((ulong)count);
    }

    [Fact]
    public async Task Read_Many_Should_Return_In_Stream_Order()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        
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
        
        var syncResult = await store.ReadMany(Direction.Forwards, half, CancellationToken)
            .SelectAwait(s => s.ToListAsync())
            .ToListAsync();
        var asyncResult = await store.ReadMany(Direction.Forwards, half.ToAsyncEnumerable(), CancellationToken)
            .SelectAwait(s => s.ToListAsync())
            .ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            
            result.ShouldNotBeEmpty();
            Assert.All(result, stream =>
            {
                stream.ShouldNotBeEmpty();
                stream.Should().BeInAscendingOrder(r => r.StreamPosition);
                stream.ShouldAllBe(s => s.StreamId == stream[0].StreamId);
            });
        });
        
        syncResult = await store.ReadMany(Direction.Backwards, half, CancellationToken)
            .SelectAwait(s => s.ToListAsync())
            .ToListAsync();
        asyncResult = await store.ReadMany(Direction.Backwards, half.ToAsyncEnumerable(), CancellationToken)
            .SelectAwait(s => s.ToListAsync())
            .ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            result.ShouldNotBeEmpty();
            Assert.All(result, stream =>
            {
                stream.ShouldNotBeEmpty();
                stream.Should().BeInDescendingOrder(r => r.StreamPosition);
                stream.ShouldAllBe(s => s.StreamId == stream[0].StreamId);
            });
        });
    }

    [Fact] 
    public async Task Read_Many_Should_Return_All()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        
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
        
        var syncResult = await store.ReadMany(Direction.Forwards, half, CancellationToken)
            .SelectAwait(l => l.ToListAsync())
            .ToListAsync();
        var asyncResult = await store.ReadMany(Direction.Forwards, half.ToAsyncEnumerable(), CancellationToken)
            .SelectAwait(l => l.ToListAsync())
            .ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            result.ShouldNotBeEmpty();
            result.Count.ShouldBe(5);
            result.ShouldAllBe(l => l.Count == 10);
        });
        
        syncResult = await store.ReadMany(Direction.Backwards, half, CancellationToken)
            .SelectAwait(l => l.ToListAsync())
            .ToListAsync();
        asyncResult = await store.ReadMany(Direction.Backwards, half.ToAsyncEnumerable(), CancellationToken)
            .SelectAwait(l => l.ToListAsync())
            .ToListAsync();
        Assert.All(new [] { syncResult, asyncResult }, result =>
        {
            result.ShouldNotBeEmpty();
            result.Count.ShouldBe(5);
            result.ShouldAllBe(l => l.Count == 10);
        });
    }

    protected static async Task AssertEqual(IEnumerable<UncommittedEvent> source, Func<Direction, IAsyncEnumerable<EventEnvelope>> readEvents)
    {
        var list = source.ToList();
        await AssertEqual(list, readEvents(Direction.Forwards));
        
        list.Reverse();
        await AssertEqual(list, readEvents(Direction.Backwards));
    }
    
    private static async Task AssertEqual(IEnumerable<UncommittedEvent> source, IAsyncEnumerable<EventEnvelope> events)
    {
        source = source.ToList();
        var other = await events.ToListAsync();
        
        other.Select(e => ((Event)e.Event).Id).Should().BeEquivalentTo(
            source.Select(e => ((Event)e.Event).Id));
        
        Assert.All(other, o =>
        {
            o.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
            o.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        });
    }

    private static ulong RandVersion(int? min = null) => (ulong) Random.Shared.Next(min + 1 ?? 0, int.MaxValue - 1);

    protected static UncommittedEvent NewEvent()
    {
        var id = Guid.NewGuid();
        return new UncommittedEvent(new Event(id))
        {
            Metadata = new Metadata()
        };
    }

    public record Event(Guid Id);

    protected record Metadata;
}