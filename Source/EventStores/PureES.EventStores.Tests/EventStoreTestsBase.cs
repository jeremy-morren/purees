using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace PureES.EventStores.Tests;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
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
        ArgumentNullException.ThrowIfNull(testName);
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
        return CreateStore($"{testName}-{Environment.Version}", configureServices, ct);
    }

    [DebuggerNonUserCode]
    protected static CancellationToken CancellationToken => new CancellationTokenSource(TimeSpan.FromMinutes(10)).Token;
    
    [Fact]
    public async Task Create()
    {
        var start = DateTime.UtcNow;
        
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (uint) events.Count - 1;
        (await store.Exists(stream, CancellationToken)).ShouldBeFalse();

        (await store.Create(stream, events, CancellationToken)).ShouldBe(revision);
        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);
        (await store.Exists(stream, CancellationToken)).ShouldBeTrue();
        
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));

        (await store.GetRevision(stream, CancellationToken)).ShouldBe((uint)events.Count - 1);
        
        (await store.ReadAll(Direction.Forwards, CancellationToken).GroupBy(s => s.Timestamp).CountAsync())
            .ShouldBe(1, "Created events should have the same timestamp");

        var ts = await store.ReadAll(Direction.Forwards, CancellationToken)
            .GroupBy(s => s.Timestamp)
            .Select(s => s.Key)
            .SingleAsync();
        ts.Kind.ShouldBe(DateTimeKind.Utc);
        ts.Should().BeAfter(start).And.BeBefore(DateTime.UtcNow, "Timestamp should be set to now");
    }
    
    [Fact]
    public async Task Create_Single()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create_Single);
        var @event = NewEvent();
        const uint revision = 0;
        (await store.Exists(stream, CancellationToken)).ShouldBeFalse();
        (await store.Create(stream, @event, CancellationToken)).ShouldBe(revision);

        (await store.Exists(stream, CancellationToken)).ShouldBeTrue();
        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);
        
        await AssertEqual([@event], d => store.Read(d, stream, CancellationToken));
        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);
    }

    [Fact]
    public async Task Submit_Transaction()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        var transaction = new EventsTransaction();
        var overallOrder = new List<(string StreamId, uint StreamPosition)>();

        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken);

        (await store.ReadAll(Direction.Forwards, CancellationToken).GroupBy(s => s.Timestamp).CountAsync())
            .ShouldBe(1, "All events in a transaction should have the same timestamp");

        await VerifyTransaction();

        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), (uint)i, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(5, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));

        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken);
        await VerifyTransaction();

        var totalCount = 0;
        foreach (var i in Enumerable.Range(0, 5))
        {
            var count = (i + 1) * 2;
            (await store.GetRevision(i.ToString(), CancellationToken)).ShouldBe((uint)count - 1);
            (await store.Read(Direction.Forwards, i.ToString(), CancellationToken).ToListAsync()).Should().HaveCount(count);
            totalCount += count;
        }

        foreach (var i in Enumerable.Range(5, 5))
        {
            var count = i + 1;
            (await store.GetRevision(i.ToString(), CancellationToken)).ShouldBe((uint)count - 1);
            (await store.Read(Direction.Forwards, i.ToString(), CancellationToken).ToListAsync())
                .Should().HaveCount(count);
            totalCount += count;
        }
        
        (await store.ReadAll(Direction.Forwards, CancellationToken).ToListAsync()).Should().HaveCount(totalCount);
        (await store.Count(CancellationToken)).ShouldBe((uint)totalCount);

        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), (uint)i, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(5, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(10, 5))
            transaction.Add(i.ToString(), (uint)i + 1, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        foreach (var i in Enumerable.Range(15, 5)) //Valid, test atomicity
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        var ex = await Assert.ThrowsAsync<EventsTransactionException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken));

        ex.InnerExceptions.Should().HaveCount(15);
        ex.InnerExceptions.OfType<WrongStreamRevisionException>().Should().HaveCount(5);
        ex.InnerExceptions.OfType<StreamAlreadyExistsException>().Should().HaveCount(5);
        ex.InnerExceptions.OfType<StreamNotFoundException>().Should().HaveCount(5);
        
        //Ensure the valid ones weren't committed
        await Assert.AllAsync(Enumerable.Range(15, 5), async i =>
            (await store.Exists(i.ToString(), CancellationToken)).ShouldBeFalse());

        return;

        //Adds to overall order, clears transaction and verifies the overall order
        async Task VerifyTransaction()
        {
            foreach (var (streamId, events) in transaction)
            {
                var index = (int?)(events.ExpectedRevision + 1) ?? 0;
                overallOrder.AddRange(Enumerable.Range(index, events.Count).Select(i => (streamId, (uint)i)));
            }
            transaction.Clear();

            (await store.ReadAll(Direction.Forwards, CancellationToken)
                    .Select(e => (e.StreamId, e.StreamPosition))
                    .ToListAsync())
                .Should().BeEquivalentTo(overallOrder, "Read all should preserve transaction order");

            (await store.ReadAll(Direction.Backwards, CancellationToken)
                    .Select(e => (e.StreamId, e.StreamPosition))
                    .ToListAsync())
                .Should().BeEquivalentTo(overallOrder.AsEnumerable().Reverse(), "Read all should preserve transaction order");
        }
    }
    
    [Fact]
    public async Task Submit_Transaction_With_Single_Error_Should_Throw_That_Error()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        const string streamId = nameof(Submit_Transaction_With_Single_Error_Should_Throw_That_Error);
        await store.Create(streamId, NewEvent(), CancellationToken);
        
        var transaction = new EventsTransaction();
        transaction.Add(streamId, null, NewEvent());
        
        var exists = await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken));
        exists.StreamId.ShouldBe(streamId);
        
        transaction.Clear();
        transaction.Add(streamId, 1, NewEvent());
        var wrongRevision = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken));
        wrongRevision.StreamId.ShouldBe(streamId);
        wrongRevision.ExpectedRevision.ShouldBe(1u);
        wrongRevision.ActualRevision.ShouldBe(0u);
        
        transaction.Clear();
        transaction.Add("NotFound", 2, NewEvent());
        var notFound = await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), CancellationToken));
        notFound.StreamId.ShouldBe("NotFound");
    }

    [Fact]
    public async Task Create_Single_Existing_Should_Throw()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create_Existing_Should_Throw);
        const uint revision = 0;
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

        (await store.Create(stream, events, CancellationToken)).ShouldBe((uint) events.Count - 1);
        
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
        (await store.Create(stream, events.Take(5), CancellationToken)).ShouldBe(4u);

        if (useOptimisticConcurrency)
            (await store.Append(stream, 4, events.Skip(5), CancellationToken)).ShouldBe(9u);
        else
            (await store.Append(stream, events.Skip(5), CancellationToken)).ShouldBe(9u);
        
        await AssertEqual(events, d => store.Read(d, stream, CancellationToken));

        store.Read(Direction.Backwards, stream, CancellationToken).StreamId.ShouldBe(stream);
        (await store.GetRevision(stream, CancellationToken)).ShouldBe((uint)events.Count - 1);
        
        (await store.ReadAll(Direction.Forwards, CancellationToken)
                .Skip(5)
                .GroupBy(s => s.Timestamp)
                .CountAsync())
            .ShouldBe(1, "Appended events should have the same timestamp");
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
                ? store.Append(stream, 0, [NewEvent()], CancellationToken)
                : store.Append(stream, [NewEvent()], CancellationToken));
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
        var revision = (uint) events.Count - 1;
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
        (await store.Create(stream, @event, CancellationToken)).ShouldBe(0u);
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.Append(stream, 1, NewEvent(), CancellationToken));
        ex.ActualRevision.ShouldBe(0u);
    }

    [Fact]
    public async Task Create_Append_With_Null_Metadata_Should_Succeed()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        
        const string stream = nameof(Create_Append_With_Null_Metadata_Should_Succeed);
        
        await store.Create(stream, NewEvent(false), CancellationToken);
        await store.Append(stream, NewEvent(false), CancellationToken);

        (await store.ReadAll(CancellationToken).ToListAsync())
            .Should().HaveCount(2).And.AllSatisfy(e => e.Metadata.ShouldBeNull());
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
        var revision = (uint) events.Count - 1;
        (await store.Create(stream, events, CancellationToken)).ShouldBe(revision);
        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);

        await AssertEqual(events, d => store.Read(d, stream, revision, CancellationToken));
        
        await AssertEqual(events.Skip(2), d => store.Read(d, stream, 2, revision, CancellationToken));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.Read(Direction.Forwards, stream, 3, 2, CancellationToken)
                .ToListAsync().AsTask());

        await AssertEqual(events.Take(5), store.ReadPartial(Direction.Forwards, stream, 5, CancellationToken));
        
        await AssertEqual(events, store.ReadPartial(Direction.Forwards, stream, 10, CancellationToken));
        
        await AssertEqual(events.TakeLast(3).Reverse(), store.ReadPartial(Direction.Backwards, stream, 3, CancellationToken));
        
        await AssertEqual(events, store.ReadSlice(stream, 0, CancellationToken));
        
        await AssertEqual(events.Skip(5), store.ReadSlice(stream, 5, CancellationToken));
        
        await AssertEqual(events.Take(3), store.ReadSlice(stream, 0, 2, CancellationToken));
        
        await AssertEqual(events.Skip(4).Take(2), store.ReadSlice(stream, 4, 5, CancellationToken));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            store.ReadSlice(stream, 3, 2, CancellationToken)
                .ToListAsync().AsTask());

        //Reading existing stream starting from wrong revision should throw wrong version, not stream not found

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
            store.ReadSlice(stream, RandVersion(events.Count + 1), int.MaxValue, CancellationToken));
        
        await AssertWrongVersion(_ => 
            store.ReadSlice(stream, RandVersion(events.Count + 1), CancellationToken));

        await AssertWrongVersion(d =>
            store.Read(d, stream, RandVersion(events.Count + 1) % (uint)short.MaxValue, int.MaxValue, CancellationToken));

        return;
        
        async Task AssertWrongVersion(Func<Direction, IAsyncEnumerable<EventEnvelope>> getEvents)
        {
            var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () =>
                await getEvents(Direction.Forwards).CountAsync());
            ex.ActualRevision.ShouldBe(revision);
            
            ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () => 
                await getEvents(Direction.Backwards).CountAsync());
            
            ex.ActualRevision.ShouldBe(revision);
        }
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
        var revision = (uint) events.Count - 1;

        (await store.GetRevision(stream, CancellationToken)).ShouldBe(revision);
        (await store.GetRevision(stream, revision, CancellationToken)).ShouldBe(revision);
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(
            () => store.GetRevision(stream, revision + 1, CancellationToken));
        ex.ExpectedRevision.ShouldBe(revision + 1);
        ex.ActualRevision.ShouldBe(revision);
        ex.StreamId.ShouldBe(stream);
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
        var revision = (uint) events.Count - 1;
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
        
        (await store.Create(stream, events.Take(count / 2), CancellationToken)).ShouldBe((uint)count / 2 - 1);
        (await store.Append(stream, events.Skip(count / 2), CancellationToken)).ShouldBe((uint)count - 1);

        (await store.GetRevision(stream, CancellationToken)).ShouldBe((uint)count - 1);
        
        var list = await store.Read(Direction.Forwards, stream, CancellationToken).ToListAsync();

        list[0].StreamPosition.ShouldBe(0u);
        list[^1].StreamPosition.ShouldBe((uint)count - 1);
        list.Should().BeInAscendingOrder(s => s.StreamPosition);
        list.Should().BeInAscendingOrder(s => s.Timestamp);
        
        list.Select(e => e.StreamPosition).Should()
            .BeEquivalentTo(Enumerable.Range(0, count));
        
        list = await store.Read(Direction.Backwards, stream, CancellationToken).ToListAsync();

        list[0].StreamPosition.ShouldBe((uint)count - 1);
        list[^1].StreamPosition.ShouldBe(0u);
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
            var eventsDerived = Enumerable.Range(0, count).Select(_ => NewEventDerived());
            await store.Create($"{i}-{nameof(ReadByEventType)}", events, CancellationToken);
            await store.Create($"{i}-derived-{nameof(ReadByEventType)}", eventsDerived, CancellationToken);
        }

        var forwards = await store.ReadByEventType(Direction.Forwards, [typeof(Event)], CancellationToken).ToListAsync();
        
        forwards.ShouldNotBeEmpty();
        forwards.Should().HaveCount(count * count * 2, "Read by event type should include derived types");
        forwards.Should().BeInAscendingOrder(e => e.Timestamp);
        forwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);
        
        var backwards = await store.ReadByEventType(Direction.Backwards, [typeof(Event)], CancellationToken).ToListAsync();

        backwards.ShouldNotBeEmpty();
        backwards.Should().HaveCount(count * count * 2, "Read by event type should include derived types");
        backwards.Should().BeInDescendingOrder(e => e.Timestamp);
        backwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);
        
        forwards = await store.ReadByEventType(Direction.Forwards, [typeof(EventDerived)], CancellationToken).ToListAsync();
        forwards.ShouldNotBeEmpty();
        forwards.Should().HaveCount(count * count);
        forwards.Should().BeInAscendingOrder(e => e.Timestamp);
        forwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);
        
        backwards = await store.ReadByEventType(Direction.Backwards, [typeof(Event), typeof(EventDerived)], CancellationToken).ToListAsync();
        backwards.ShouldNotBeEmpty();
        backwards.Should().HaveCount(count * count * 2);
        backwards.Should().BeInDescendingOrder(e => e.Timestamp);
        backwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);

        (await store.ReadByEventType(Direction.Forwards, [typeof(Event)], 1, CancellationToken).ToListAsync())
            .ShouldHaveSingleItem();
        (await store.ReadByEventType(Direction.Forwards, [typeof(EventDerived)], 1, CancellationToken).ToListAsync())
            .ShouldHaveSingleItem();
        
        (await store.ReadByEventType(Direction.Backwards, [typeof(Event)], 1, CancellationToken).ToListAsync())
            .ShouldHaveSingleItem();
        (await store.ReadByEventType(Direction.Backwards, [typeof(EventDerived)], 1, CancellationToken).ToListAsync())
            .ShouldHaveSingleItem();

        (await store.ReadByEventType(Direction.Forwards, [], CancellationToken).ToListAsync()).ShouldBeEmpty();
        (await store.ReadByEventType(Direction.Forwards, [], 1, CancellationToken).ToListAsync()).ShouldBeEmpty();
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

        (await store.Count(CancellationToken)).ShouldBe((uint)count * count);
    }
    
    [Fact]
    public async Task CountByEventType()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        const string stream = nameof(CountByEventType);
        const string streamDerived = $"{nameof(CountByEventType)}-derived";
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        var eventsDerived = Enumerable.Range(0, count).Select(_ => NewEventDerived()).ToList();
        
        await store.Create(stream, events, CancellationToken);
        await store.Create(streamDerived, eventsDerived, CancellationToken);
        
        (await store.CountByEventType([typeof(EventDerived)], CancellationToken)).ShouldBe((uint)count);
        (await store.CountByEventType([typeof(Event)], CancellationToken))
            .ShouldBe((uint)count  * 2, "Count events should include derived types");
        (await store.CountByEventType([typeof(Event), typeof(EventDerived)], CancellationToken)).ShouldBe((uint)count * 2);
        
        (await store.CountByEventType([], CancellationToken)).ShouldBe(0u);
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
        Assert.All([syncResult, asyncResult], result =>
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
        Assert.All([syncResult, asyncResult], result =>
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
            .SelectAwait(async l => new
            {
                l.StreamId,
                Events = await l.ToListAsync()
            })
            .ToListAsync();
        var asyncResult = await store.ReadMany(Direction.Forwards, half.ToAsyncEnumerable(), CancellationToken)
            .SelectAwait(async l => new
            {
                l.StreamId,
                Events = await l.ToListAsync()
            })
            .ToListAsync();

        Assert.All([syncResult, asyncResult], result =>
        {
            result.ShouldNotBeEmpty();
            result.Count.ShouldBe(5);
            result.ShouldAllBe(l => l.Events.Count == 10);
            result
                .Should().AllSatisfy(x =>
                {
                    x.Events.Should().HaveCount(10).And.BeInAscendingOrder(e => e.StreamPosition);
                    x.Events.Select(e => e.StreamId).Should().AllBe(x.StreamId);
                });
            result.Select(e => e.StreamId).Should()
                .BeEquivalentTo(half, o => o.WithoutStrictOrdering());
        });
        
        syncResult = await store.ReadMany(Direction.Backwards, half, CancellationToken)
            .SelectAwait(async l => new
            {
                l.StreamId,
                Events = await l.ToListAsync()
            })
            .ToListAsync();
        asyncResult = await store.ReadMany(Direction.Backwards, half.ToAsyncEnumerable(), CancellationToken)
            .SelectAwait(async l => new
            {
                l.StreamId,
                Events = await l.ToListAsync()
            })
            .ToListAsync();
        Assert.All([syncResult, asyncResult], result =>
        {
            result.ShouldNotBeEmpty();
            result.Count.ShouldBe(5);
            result
                .Should().AllSatisfy(x =>
                {
                    x.Events.Should().HaveCount(10).And.BeInDescendingOrder(e => e.StreamPosition);
                    x.Events.Select(e => e.StreamId).Should().AllBe(x.StreamId);
                });
            result.Select(e => e.StreamId).Should()
                .BeEquivalentTo(half, o => o.WithoutStrictOrdering());
        });
    }

    [Fact]
    public async Task Read_Many_Should_Throw_For_Invalid_Streams()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        await Assert.AllAsync(Enum.GetValues<Direction>(), async direction =>
        {
            var ex = await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
                await store.ReadMany(direction, ["stream"], CancellationToken).ToListAsync());
            ex.StreamId.ShouldBe("stream");

            var aggregateEx = await Assert.ThrowsAsync<AggregateException>(async () =>
                await store.ReadMany(direction, ["stream1", "stream2"], CancellationToken).ToListAsync());

            aggregateEx.InnerExceptions.Should().HaveCount(2);
            aggregateEx.InnerExceptions.Should().AllBeOfType<StreamNotFoundException>();
            aggregateEx.InnerExceptions.OfType<StreamNotFoundException>()
                .Select(e => e.StreamId)
                .Should().BeEquivalentTo("stream1", "stream2");
        });

        await Assert.AllAsync(Enum.GetValues<Direction>(), async direction =>
        {
            var ex = await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
                await store.ReadMany(direction,
                    new [] { "stream"}.ToAsyncEnumerable(),
                    CancellationToken).ToListAsync());

            ex.StreamId.ShouldBe("stream");

            var aggregateEx = await Assert.ThrowsAsync<AggregateException>(async () =>
                await store.ReadMany(direction,
                    new [] {"stream1", "stream2"}.ToAsyncEnumerable(),
                    CancellationToken).ToListAsync());

            aggregateEx.InnerExceptions.Should().HaveCount(2);
            aggregateEx.InnerExceptions.Should().AllBeOfType<StreamNotFoundException>();
            aggregateEx.InnerExceptions.OfType<StreamNotFoundException>()
                .Select(e => e.StreamId)
                .Should().BeEquivalentTo("stream1", "stream2");
        });
    }

    [Fact]
    public async Task Read_Many_Should_Return_Each_Stream_Only_Once()
    {
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        await Assert.AllAsync(Enum.GetValues<Direction>(), async direction =>
        {
            var ex = await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
                await store.ReadMany(direction, ["stream"], CancellationToken).ToListAsync());
            ex.StreamId.ShouldBe("stream");

            var aggregateEx = await Assert.ThrowsAsync<AggregateException>(async () =>
                await store.ReadMany(direction, ["stream1", "stream2"], CancellationToken).ToListAsync());

            aggregateEx.InnerExceptions.Should().HaveCount(2);
            aggregateEx.InnerExceptions.Should().AllBeOfType<StreamNotFoundException>();
            aggregateEx.InnerExceptions.OfType<StreamNotFoundException>()
                .Select(e => e.StreamId)
                .Should().BeEquivalentTo("stream1", "stream2");
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

        other.Select(e => Props(e.Event, e.Metadata))
            .Should().BeEquivalentTo(
                source.Select(e => Props(e.Event, e.Metadata)),
                o => o.WithStrictOrdering());
        
        other.Should().AllSatisfy(o =>
        {
            o.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
            o.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        });

        return;

        object Props(object @event, object? metadata) => new
        {
            EventId = @event.ShouldBeOfType<Event>().Id,
            Metadata = metadata != null
        };
    }

    private static uint RandVersion(int? min = null) => (uint) Random.Shared.Next(min + 1 ?? 0, int.MaxValue - 1);

    protected static UncommittedEvent NewEvent(bool includeMetadata = true)
    {
        var id = Guid.NewGuid();
        return new UncommittedEvent(new Event(id))
        {
            Metadata = includeMetadata ? new Metadata() : null
        };
    }
    
    private static UncommittedEvent NewEventDerived()
    {
        var id = Guid.NewGuid();
        return new UncommittedEvent(new EventDerived(id))
        {
            Metadata = new Metadata()
        };
    }

    protected record Event(Guid Id);
    
    private record EventDerived(Guid Id) : Event(Id);

    private record Metadata;
}