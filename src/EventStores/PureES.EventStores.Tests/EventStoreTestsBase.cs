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
        var ct = TestContext.Current.CancellationToken;
        return CreateStore($"{testName}-{Environment.Version}", configureServices, ct);
    }

    [Fact]
    public async Task Create()
    {
        var ct = TestContext.Current.CancellationToken;

        var start = DateTime.UtcNow;
        
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (uint) events.Count - 1;
        (await store.Exists(stream, ct)).ShouldBeFalse();

        (await store.Create(stream, events, ct)).ShouldBe(revision);
        (await store.GetRevision(stream, ct)).ShouldBe(revision);
        (await store.Exists(stream, ct)).ShouldBeTrue();

        await AssertEqual(events, d => store.Read(d, stream, ct));

        (await store.GetRevision(stream, ct)).ShouldBe((uint)events.Count - 1);

        (await store.ReadAll(Direction.Forwards, ct).GroupBy(s => s.Timestamp).CountAsync(ct))
            .ShouldBe(1, "Created events should have the same timestamp");

        var ts = await store.ReadAll(Direction.Forwards, ct)
            .GroupBy(s => s.Timestamp)
            .Select(s => s.Key)
            .SingleAsync(ct);
        ts.Kind.ShouldBe(DateTimeKind.Utc);
        ts.Should().BeAfter(start).And.BeBefore(DateTime.UtcNow, "Timestamp should be set to now");
    }
    
    [Fact]
    public async Task Create_Single()
    {
        var ct = TestContext.Current.CancellationToken;
        
        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create_Single);
        var @event = NewEvent();
        const uint revision = 0;
        (await store.Exists(stream, ct)).ShouldBeFalse();
        (await store.Create(stream, @event, ct)).ShouldBe(revision);

        (await store.Exists(stream, ct)).ShouldBeTrue();
        (await store.GetRevision(stream, ct)).ShouldBe(revision);

        await AssertEqual([@event], d => store.Read(d, stream, ct));
        (await store.GetRevision(stream, ct)).ShouldBe(revision);
    }

    [Fact]
    public async Task Submit_Transaction()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        var transaction = new EventsTransaction();
        var overallOrder = new List<(string StreamId, uint StreamPosition)>();

        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), ct);

        (await store.ReadAll(Direction.Forwards, ct).GroupBy(s => s.Timestamp).CountAsync(ct))
            .ShouldBe(1, "All events in a transaction should have the same timestamp");

        await VerifyTransaction();

        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), (uint)i, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(5, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));

        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), ct);
        await VerifyTransaction();

        var totalCount = 0;
        foreach (var i in Enumerable.Range(0, 5))
        {
            var count = (i + 1) * 2;
            (await store.GetRevision(i.ToString(), ct)).ShouldBe((uint)count - 1);
            (await store.Read(Direction.Forwards, i.ToString(), ct).ToListAsync(ct)).Should().HaveCount(count);
            totalCount += count;
        }

        foreach (var i in Enumerable.Range(5, 5))
        {
            var count = i + 1;
            (await store.GetRevision(i.ToString(), ct)).ShouldBe((uint)count - 1);
            (await store.Read(Direction.Forwards, i.ToString(), ct).ToListAsync(ct))
                .Should().HaveCount(count);
            totalCount += count;
        }
        
        (await store.ReadAll(Direction.Forwards, ct).ToListAsync(ct)).Should().HaveCount(totalCount);
        (await store.Count(ct)).ShouldBe((uint)totalCount);

        foreach (var i in Enumerable.Range(0, 5))
            transaction.Add(i.ToString(), (uint)i, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(5, 5))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        foreach (var i in Enumerable.Range(10, 5))
            transaction.Add(i.ToString(), (uint)i + 1, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        foreach (var i in Enumerable.Range(15, 5)) //Valid, test atomicity
            transaction.Add(i.ToString(), null, Enumerable.Range(0, i + 1).Select(_ => NewEvent()));
        
        var ex = await Assert.ThrowsAsync<EventsTransactionException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), ct));

        ex.InnerExceptions.Should().HaveCount(15);
        ex.InnerExceptions.OfType<WrongStreamRevisionException>().Should().HaveCount(5);
        ex.InnerExceptions.OfType<StreamAlreadyExistsException>().Should().HaveCount(5);
        ex.InnerExceptions.OfType<StreamNotFoundException>().Should().HaveCount(5);
        
        //Ensure the valid ones weren't committed
        await Assert.AllAsync(Enumerable.Range(15, 5), async i =>
            (await store.Exists(i.ToString(), ct)).ShouldBeFalse());

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

            (await store.ReadAll(Direction.Forwards, ct)
                    .Select(e => (e.StreamId, e.StreamPosition))
                    .ToListAsync(ct))
                .Should().BeEquivalentTo(overallOrder, "Read all should preserve transaction order");

            (await store.ReadAll(Direction.Backwards, ct)
                    .Select(e => (e.StreamId, e.StreamPosition))
                    .ToListAsync(ct))
                .Should().BeEquivalentTo(overallOrder.AsEnumerable().Reverse(), "Read all should preserve transaction order");
        }
    }
    
    [Fact]
    public async Task Submit_Transaction_With_Single_Error_Should_Throw_That_Error()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        const string streamId = nameof(Submit_Transaction_With_Single_Error_Should_Throw_That_Error);
        await store.Create(streamId, NewEvent(), ct);
        
        var transaction = new EventsTransaction
        {
            { streamId, null, NewEvent() }
        };

        var exists = await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), ct));
        exists.StreamId.ShouldBe(streamId);
        
        transaction.Clear();
        transaction.Add(streamId, 1, NewEvent());
        var wrongRevision = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), ct));
        wrongRevision.StreamId.ShouldBe(streamId);
        wrongRevision.ExpectedRevision.ShouldBe(1u);
        wrongRevision.ActualRevision.ShouldBe(0u);
        
        transaction.Clear();
        transaction.Add("NotFound", 2, NewEvent());
        var notFound = await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            store.SubmitTransaction(transaction.ToUncommittedTransaction(), ct));
        notFound.StreamId.ShouldBe("NotFound");
    }

    [Fact]
    public async Task Create_Single_Existing_Should_Throw()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create_Existing_Should_Throw);
        const uint revision = 0;
        (await store.Create(stream, NewEvent(), ct)).ShouldBe(revision);
        
        await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.Create(stream, NewEvent(), ct));
    }
    
    [Fact]
    public async Task Create_Existing_Should_Throw()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Create);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();

        (await store.Create(stream, events, ct)).ShouldBe((uint) events.Count - 1);
        
        var ex = await Assert.ThrowsAsync<StreamAlreadyExistsException>(() =>
            store.Create(stream, events, ct));
        ex.StreamId.ShouldBe(stream);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append(bool useOptimisticConcurrency)
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness($"{nameof(Append)}+{useOptimisticConcurrency}");
        var store = harness.EventStore;
        const string stream = nameof(Append);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        (await store.Create(stream, events.Take(5), ct)).ShouldBe(4u);

        if (useOptimisticConcurrency)
            (await store.Append(stream, 4, events.Skip(5), ct)).ShouldBe(9u);
        else
            (await store.Append(stream, events.Skip(5), ct)).ShouldBe(9u);
        
        await AssertEqual(events, d => store.Read(d, stream, ct));

        store.Read(Direction.Backwards, stream, ct).StreamId.ShouldBe(stream);
        (await store.GetRevision(stream, ct)).ShouldBe((uint)events.Count - 1);
        
        (await store.ReadAll(Direction.Forwards, ct)
                .Skip(5)
                .GroupBy(s => s.Timestamp)
                .CountAsync(ct))
            .ShouldBe(1, "Appended events should have the same timestamp");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append_To_Invalid_Should_Throw(bool useOptimisticConcurrency)
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness($"{nameof(Append_To_Invalid_Should_Throw)}+{useOptimisticConcurrency}");
        var store = harness.EventStore;
        const string stream = nameof(Append_To_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            useOptimisticConcurrency
                ? store.Append(stream, 0, NewEvent(), ct)
                : store.Append(stream, NewEvent(), ct));
        
        await Assert.ThrowsAsync<StreamNotFoundException>(() =>
            useOptimisticConcurrency
                ? store.Append(stream, 0, [NewEvent()], ct)
                : store.Append(stream, [NewEvent()], ct));
    }

    [Fact]
    public async Task Append_With_Invalid_Revision_Should_Throw()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Append_With_Invalid_Revision_Should_Throw);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (uint) events.Count - 1;
        (await store.Create(stream, events, ct)).ShouldBe(revision);
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.Append(stream, RandVersion(events.Count + 1), events, ct));
        ex.ActualRevision.ShouldBe(revision);
    }
    
    [Fact]
    public async Task Append_Single_With_Invalid_Revision_Should_Throw()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Append_With_Invalid_Revision_Should_Throw);
        var @event = NewEvent();
        (await store.Create(stream, @event, ct)).ShouldBe(0u);
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(() =>
            store.Append(stream, 1, NewEvent(), ct));
        ex.ActualRevision.ShouldBe(0u);
    }

    [Fact]
    public async Task Create_Append_With_Null_Metadata_Should_Succeed()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        
        const string stream = nameof(Create_Append_With_Null_Metadata_Should_Succeed);
        
        await store.Create(stream, NewEvent(false), ct);
        await store.Append(stream, NewEvent(false), ct);

        (await store.ReadAll(ct).ToListAsync(ct))
            .Should().HaveCount(2).And.AllSatisfy(e => e.Metadata.ShouldBeNull());
    }

    [Fact]
    public async Task Read_Invalid_Stream_Should_Throw()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Read_Invalid_Stream_Should_Throw);
        Assert.False(await store.Exists(stream, ct));
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, ct));
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () => 
            await store.Read(Direction.Forwards, stream, ct).FirstAsync(ct));
        await Assert.ThrowsAsync<StreamNotFoundException>(async () => 
            await store.Read(Direction.Backwards, stream, ct).FirstAsync(ct));
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.Read(Direction.Forwards, stream, RandVersion(), ct).FirstAsync(ct));
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.Read(Direction.Backwards, stream, RandVersion(), ct).FirstAsync(ct));
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.Read(Direction.Backwards, stream, RandVersion() % (int)short.MaxValue, RandVersion(short.MaxValue), ct).FirstAsync(ct));
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadPartial(Direction.Forwards, stream, RandVersion(), ct).FirstAsync(ct));
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadPartial(Direction.Backwards, stream, RandVersion(), ct).FirstAsync(ct));
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadSlice(stream, RandVersion() % (int)short.MaxValue, ct).FirstAsync(ct));
        
        await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
            await store.ReadSlice(stream, RandVersion() % (int)short.MaxValue, RandVersion(short.MaxValue), ct).FirstAsync(ct));
    }

    [Fact]
    public async Task Read()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Read);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (uint) events.Count - 1;
        (await store.Create(stream, events, ct)).ShouldBe(revision);
        (await store.GetRevision(stream, ct)).ShouldBe(revision);

        await AssertEqual(events, d => store.Read(d, stream, revision, ct));
        
        await AssertEqual(events.Skip(2), d => store.Read(d, stream, 2, revision, ct));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.Read(Direction.Forwards, stream, 3, 2, ct)
                .ToListAsync(ct).AsTask());

        await AssertEqual(events.Take(5), store.ReadPartial(Direction.Forwards, stream, 5, ct));
        
        await AssertEqual(events, store.ReadPartial(Direction.Forwards, stream, 10, ct));
        
        await AssertEqual(events.TakeLast(3).Reverse(), store.ReadPartial(Direction.Backwards, stream, 3, ct));
        
        await AssertEqual(events, store.ReadSlice(stream, 0, ct));
        
        await AssertEqual(events.Skip(5), store.ReadSlice(stream, 5, ct));
        
        await AssertEqual(events.Take(3), store.ReadSlice(stream, 0, 2, ct));
        
        await AssertEqual(events.Skip(4).Take(2), store.ReadSlice(stream, 4, 5, ct));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            store.ReadSlice(stream, 3, 2, ct)
                .ToListAsync(ct).AsTask());

        //Reading existing stream starting from wrong revision should throw wrong version, not stream not found

        await AssertWrongVersion(d => 
            store.Read(d, stream, RandVersion(events.Count + 1), ct));

        await AssertWrongVersion(d => 
            store.Read(d, stream, 0, ct));

        await AssertWrongVersion(d => 
            store.Read(d, stream, RandVersion(events.Count + 1), ct));

        await AssertWrongVersion(d =>
            store.Read(d, stream, 2, RandVersion(events.Count + 1), ct));

        await AssertWrongVersion(d => 
            store.ReadPartial(d, stream, RandVersion(events.Count + 1), ct));
        
        await AssertWrongVersion(_ => 
            store.ReadSlice(stream, 1, RandVersion(events.Count + 1), ct));
        
        await AssertWrongVersion(_ => 
            store.ReadSlice(stream, RandVersion(events.Count + 1), int.MaxValue, ct));
        
        await AssertWrongVersion(_ => 
            store.ReadSlice(stream, RandVersion(events.Count + 1), ct));

        await AssertWrongVersion(d =>
            store.Read(d, stream, RandVersion(events.Count + 1) % (uint)short.MaxValue, int.MaxValue, ct));

        return;
        
        async Task AssertWrongVersion(Func<Direction, IAsyncEnumerable<EventEnvelope>> getEvents)
        {
            var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () =>
                await getEvents(Direction.Forwards).CountAsync(ct));
            ex.ActualRevision.ShouldBe(revision);
            
            ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(async () => 
                await getEvents(Direction.Backwards).CountAsync(ct));
            
            ex.ActualRevision.ShouldBe(revision);
        }
    }

    [Fact]
    public async Task GetStreamRevision()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(GetStreamRevision);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        await store.Create(stream, events, ct);
        var revision = (uint) events.Count - 1;

        (await store.GetRevision(stream, ct)).ShouldBe(revision);
        (await store.GetRevision(stream, revision, ct)).ShouldBe(revision);
        var ex = await Assert.ThrowsAsync<WrongStreamRevisionException>(
            () => store.GetRevision(stream, revision + 1, ct));
        ex.ExpectedRevision.ShouldBe(revision + 1);
        ex.ActualRevision.ShouldBe(revision);
        ex.StreamId.ShouldBe(stream);
    }

    [Fact]
    public async Task Get_Revision_Invalid_Should_Throw()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(Get_Revision_Invalid_Should_Throw);
        await Assert.ThrowsAsync<StreamNotFoundException>(() => store.GetRevision(stream, ct));
    }

    [Fact]
    public async Task ReadAll()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(ReadAll);
        var events = Enumerable.Range(0, 10)
            .Select(_ => NewEvent())
            .ToList();
        var revision = (uint) events.Count - 1;
        Assert.Equal(revision, await store.Create(stream, events, ct));
        Assert.Equal(revision, await store.GetRevision(stream, ct));

        var forwards = await store.ReadAll(Direction.Forwards, ct).ToListAsync(ct);
        Assert.NotEmpty(forwards);

        var sorted = forwards
            .OrderBy(e => e.Timestamp)
            .ThenBy(a => a.StreamId)
            .ThenBy(a => a.StreamPosition)
            .Select(a => $"{a.StreamId}/{a.StreamPosition}")
            .ToArray();
        
        Assert.Equal(sorted,
            forwards.Select(a => $"{a.StreamId}/{a.StreamPosition}").ToArray());

        forwards.GroupBy(a => new { a.StreamId, a.StreamPosition })
            .ShouldNotContain(g => g.Count() > 1);

        forwards.Reverse();
        var backwards = await store.ReadAll(Direction.Backwards, ct).ToListAsync(ct);

        Assert.Equal(forwards.Select(e => e.StreamId), backwards.Select(s => s.StreamId));

        Assert.Single(await store.ReadAll(Direction.Forwards, 1, ct).ToListAsync(ct));
        Assert.Single(await store.ReadAll(Direction.Backwards, 1, ct).ToListAsync(ct));
    }

    [Fact]
    public async Task StreamPosition_Should_Be_Ordered_And_Unique()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        const string stream = nameof(StreamPosition_Should_Be_Ordered_And_Unique);
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        
        (await store.Create(stream, events.Take(count / 2), ct)).ShouldBe((uint)count / 2 - 1);
        (await store.Append(stream, events.Skip(count / 2), ct)).ShouldBe((uint)count - 1);

        (await store.GetRevision(stream, ct)).ShouldBe((uint)count - 1);
        
        var list = await store.Read(Direction.Forwards, stream, ct).ToListAsync(ct);

        list[0].StreamPosition.ShouldBe(0u);
        list[^1].StreamPosition.ShouldBe((uint)count - 1);
        list.Should().BeInAscendingOrder(s => s.StreamPosition);
        list.Should().BeInAscendingOrder(s => s.Timestamp);
        
        list.Select(e => e.StreamPosition).Should()
            .BeEquivalentTo(Enumerable.Range(0, count));
        
        list = await store.Read(Direction.Backwards, stream, ct).ToListAsync(ct);

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
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;
        
        const int count = 10;
        for (var i = 0; i < count; i++)
        {
            var events = Enumerable.Range(0, count).Select(_ => NewEvent());
            var eventsDerived = Enumerable.Range(0, count).Select(_ => NewEventDerived());
            await store.Create($"{i}-{nameof(ReadByEventType)}", events, ct);
            await store.Create($"{i}-derived-{nameof(ReadByEventType)}", eventsDerived, ct);
        }

        var forwards = await store.ReadByEventType(Direction.Forwards, [typeof(Event)], ct).ToListAsync(ct);
        
        forwards.ShouldNotBeEmpty();
        forwards.Should().HaveCount(count * count * 2, "Read by event type should include derived types");
        forwards.Should().BeInAscendingOrder(e => e.Timestamp);
        forwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);
        
        var backwards = await store.ReadByEventType(Direction.Backwards, [typeof(Event)], ct).ToListAsync(ct);

        backwards.ShouldNotBeEmpty();
        backwards.Should().HaveCount(count * count * 2, "Read by event type should include derived types");
        backwards.Should().BeInDescendingOrder(e => e.Timestamp);
        backwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);
        
        forwards = await store.ReadByEventType(Direction.Forwards, [typeof(EventDerived)], ct).ToListAsync(ct);
        forwards.ShouldNotBeEmpty();
        forwards.Should().HaveCount(count * count);
        forwards.Should().BeInAscendingOrder(e => e.Timestamp);
        forwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);
        
        backwards = await store.ReadByEventType(Direction.Backwards, [typeof(Event), typeof(EventDerived)], ct).ToListAsync(ct);
        backwards.ShouldNotBeEmpty();
        backwards.Should().HaveCount(count * count * 2);
        backwards.Should().BeInDescendingOrder(e => e.Timestamp);
        backwards.GroupBy(e => e.Timestamp).Should().HaveCountGreaterThan(1);

        (await store.ReadByEventType(Direction.Forwards, [typeof(Event)], 1, ct).ToListAsync(ct))
            .ShouldHaveSingleItem();
        (await store.ReadByEventType(Direction.Forwards, [typeof(EventDerived)], 1, ct).ToListAsync(ct))
            .ShouldHaveSingleItem();
        
        (await store.ReadByEventType(Direction.Backwards, [typeof(Event)], 1, ct).ToListAsync(ct))
            .ShouldHaveSingleItem();
        (await store.ReadByEventType(Direction.Backwards, [typeof(EventDerived)], 1, ct).ToListAsync(ct))
            .ShouldHaveSingleItem();

        (await store.ReadByEventType(Direction.Forwards, [], ct).ToListAsync(ct)).ShouldBeEmpty();
        (await store.ReadByEventType(Direction.Forwards, [], 1, ct).ToListAsync(ct)).ShouldBeEmpty();
    }
    
    [Fact]
    public async Task Count()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        const int count = 10;
        
        foreach (var stream in Enumerable.Range(0, count))
            await store.Create($"stream-{stream}",
                Enumerable.Range(0, count).Select(_ => NewEvent()),
                ct);

        (await store.Count(ct)).ShouldBe((uint)count * count);
    }
    
    [Fact]
    public async Task CountByEventType()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        const string stream = nameof(CountByEventType);
        const string streamDerived = $"{nameof(CountByEventType)}-derived";
        const int count = 100;
        var events = Enumerable.Range(0, count).Select(_ => NewEvent()).ToList();
        var eventsDerived = Enumerable.Range(0, count).Select(_ => NewEventDerived()).ToList();
        
        await store.Create(stream, events, ct);
        await store.Create(streamDerived, eventsDerived, ct);
        
        (await store.CountByEventType([typeof(EventDerived)], ct)).ShouldBe((uint)count);
        (await store.CountByEventType([typeof(Event)], ct))
            .ShouldBe((uint)count  * 2, "Count events should include derived types");
        (await store.CountByEventType([typeof(Event), typeof(EventDerived)], ct)).ShouldBe((uint)count * 2);
        
        (await store.CountByEventType([], ct)).ShouldBe(0u);
    }

    [Fact]
    public async Task Read_Many_Should_Return_In_Stream_Order()
    {
        var ct = TestContext.Current.CancellationToken;

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
                await store.Append(streamId, NewEvent(), ct);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId, NewEvent(), ct);
            }
            //Append bogus streams to test filter
            try
            {
                await store.Append(streamId + "-other", NewEvent(), ct);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId + "-other", NewEvent(), ct);
            }
        }

        var half = Enumerable.Range(0, 10)
            .Select(i => $"stream-{i}")
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToList();

        var syncResult = await store.ReadMany(Direction.Forwards, half, ct)
            .Select((IEventStoreStream s,CancellationToken _) => s.ToListAsync(ct))
            .ToListAsync(ct);
        var asyncResult = await store.ReadMany(Direction.Forwards, half.ToAsyncEnumerable(), ct)
            .Select((IEventStoreStream s,CancellationToken _) => s.ToListAsync(ct))
            .ToListAsync(ct);
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
        
        syncResult = await store.ReadMany(Direction.Backwards, half, ct)
            .Select((IEventStoreStream s,CancellationToken _) => s.ToListAsync(ct))
            .ToListAsync(ct);
        asyncResult = await store.ReadMany(Direction.Backwards, half.ToAsyncEnumerable(), ct)
            .Select((IEventStoreStream s,CancellationToken _) => s.ToListAsync(ct))
            .ToListAsync(ct);
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
        var ct = TestContext.Current.CancellationToken;

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
                await store.Append(streamId, NewEvent(), ct);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId, NewEvent(), ct);
            }
            //Append bogus streams to test filter
            try
            {
                await store.Append(streamId + "-other", NewEvent(), ct);
            }
            catch (StreamNotFoundException)
            {
                await store.Create(streamId + "-other", NewEvent(), ct);
            }
        }

        var half = Enumerable.Range(0, 10)
            .Select(i => $"stream-{i}")
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToHashSet();
        
        var syncResult = await store.ReadMany(Direction.Forwards, half, ct)
            .Select(async (IEventStoreStream l, CancellationToken _) => new
            {
                l.StreamId,
                Events = await l.ToListAsync(ct)
            })
            .ToListAsync(ct);
        var asyncResult = await store.ReadMany(Direction.Forwards, half.ToAsyncEnumerable(), ct)
            .Select(async (IEventStoreStream l, CancellationToken _) => new
            {
                l.StreamId,
                Events = await l.ToListAsync(ct)
            })
            .ToListAsync(ct);

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
        
        syncResult = await store.ReadMany(Direction.Backwards, half, ct)
            .Select(async (IEventStoreStream l, CancellationToken _) => new
            {
                l.StreamId,
                Events = await l.ToListAsync(ct)
            })
            .ToListAsync(ct);
        asyncResult = await store.ReadMany(Direction.Backwards, half.ToAsyncEnumerable(), ct)
            .Select(async (IEventStoreStream l, CancellationToken _) => new
            {
                l.StreamId,
                Events = await l.ToListAsync(ct)
            })
            .ToListAsync(ct);
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
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        await Assert.AllAsync(Enum.GetValues<Direction>(), async direction =>
        {
            var ex = await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
                await store.ReadMany(direction, ["stream"], ct).ToListAsync(ct));
            ex.StreamId.ShouldBe("stream");

            var aggregateEx = await Assert.ThrowsAsync<AggregateException>(async () =>
                await store.ReadMany(direction, ["stream1", "stream2"], ct).ToListAsync(ct));

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
                    ct).ToListAsync(ct));

            ex.StreamId.ShouldBe("stream");

            var aggregateEx = await Assert.ThrowsAsync<AggregateException>(async () =>
                await store.ReadMany(direction,
                    new [] {"stream1", "stream2"}.ToAsyncEnumerable(),
                    ct).ToListAsync(ct));

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
        var ct = TestContext.Current.CancellationToken;

        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        await Assert.AllAsync(Enum.GetValues<Direction>(), async direction =>
        {
            var ex = await Assert.ThrowsAsync<StreamNotFoundException>(async () =>
                await store.ReadMany(direction, ["stream"], ct).ToListAsync(ct));
            ex.StreamId.ShouldBe("stream");

            var aggregateEx = await Assert.ThrowsAsync<AggregateException>(async () =>
                await store.ReadMany(direction, ["stream1", "stream2"], ct).ToListAsync(ct));

            aggregateEx.InnerExceptions.Should().HaveCount(2);
            aggregateEx.InnerExceptions.Should().AllBeOfType<StreamNotFoundException>();
            aggregateEx.InnerExceptions.OfType<StreamNotFoundException>()
                .Select(e => e.StreamId)
                .Should().BeEquivalentTo("stream1", "stream2");
        });
    }

    [Fact]
    public async Task Submit_Transaction_Should_Preserve_Order()
    {
        var ct = TestContext.Current.CancellationToken;

        // Ensure that events within a transaction are stored and read in the same order
        await using var harness = await CreateHarness();
        var store = harness.EventStore;

        var transaction = new EventsTransaction();
        transaction.AddOrAppend("Z", null, Event.CreateNew());
        transaction.AddOrAppend("A", null, Event.CreateNew());
        transaction.AddOrAppend("A", null, EventDerived.CreateNew());
        transaction.AddOrAppend("Z", null, Event.CreateNew(), EventDerived.CreateNew());

        Assert.Throws<InvalidOperationException>(
                () => transaction.Add("A", null, Event.CreateNew()))
            .Message.ShouldBe("Stream 'A' already exists in the transaction.");
        Assert.Throws<InvalidOperationException>(
                () => transaction.AddOrAppend("A", 1, Event.CreateNew()))
            .Message.ShouldBe("Expected revision mismatch.");

        var result = transaction.ToUncommittedTransaction();
        result.Should().HaveCount(2);
        result.Select(r => r.StreamId)
            .Should().BeEquivalentTo(["Z", "A"], "should preserve order of streams added");
        await store.SubmitTransaction(result, ct);

        await AssertEqual(
            result.SelectMany(r => r.Events),
            direction => store.ReadAll(direction, ct));

        await AssertEqual(
            result.SelectMany(r => r.Events),
            direction => store.ReadByEventType(direction, typeof(Event), ct));

        await AssertEqual(
            result.SelectMany(r => r.Events).Where(e => e.Event is EventDerived),
            direction => store.ReadByEventType(direction, typeof(EventDerived), ct));
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
            EventId = @event.ShouldBeAssignableTo<Event>().ShouldNotBeNull().Id,
            Metadata = metadata != null
        };
    }

    private static uint RandVersion(int? min = null) => (uint) Random.Shared.Next(min + 1 ?? 0, int.MaxValue - 1);

    protected static UncommittedEvent NewEvent(bool includeMetadata = true)
    {
        return new UncommittedEvent(Event.CreateNew())
        {
            Metadata = includeMetadata ? new Metadata() : null
        };
    }
    
    private static UncommittedEvent NewEventDerived()
    {
        return new UncommittedEvent(EventDerived.CreateNew())
        {
            Metadata = new Metadata()
        };
    }

    protected record Event(Guid Id)
    {
        public static Event CreateNew() => new(Guid.NewGuid());
    }

    private record EventDerived(Guid Id) : Event(Id)
    {
        public new static EventDerived CreateNew() => new(Guid.NewGuid());
    }

    private record Metadata;
}


