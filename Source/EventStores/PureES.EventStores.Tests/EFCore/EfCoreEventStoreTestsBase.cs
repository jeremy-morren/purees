using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PureES.EventStore.EFCore.Subscriptions;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable UseAwaitUsing

namespace PureES.EventStores.Tests.EFCore;

public abstract class EfCoreEventStoreTestsBase : EventStoreTestsBase
{
    [Fact]
    public async Task Subscription_To_All_Should_Handle_All_Events()
    {
        var start = DateTime.UtcNow;
        var handler = new Mock<IEventHandler>();

        var list = new List<EventEnvelope>();

        handler.Setup(s => s.CanHandle(It.Is<EventEnvelope>(e => e.Timestamp != default)))
            .Returns(true)
            .Verifiable(Times.Exactly(130));

        handler.Setup(s => s.Handle(It.IsAny<EventEnvelope>()))
            .Callback((EventEnvelope e) =>
            {
                lock (list)
                {
                    list.Add(e);
                }
            });

        await using var harness = await CreateHarness(
            s =>
            {
                s.AddPureES();
                s.AddSingleton(handler.Object);
            });

        var store = harness.EventStore;

        var subscription = harness.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<EfCoreEventStoreSubscriptionToAll>()
            .ShouldHaveSingleItem();

        await subscription.StartAsync(default); //noop

        foreach (var i in Enumerable.Range(0, 10))
            await store.Create(i.ToString(), NewEvent(), default);

        var transaction = new EventsTransaction();
        foreach (var i in Enumerable.Range(100, 10))
            transaction.Add(i.ToString(), null, Enumerable.Range(0, 10).Select(_ => NewEvent()));

        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), default);

        transaction.Clear();

        foreach (var i in Enumerable.Range(0, 10))
            transaction.Add(i.ToString(), 0, [NewEvent()]);

        await store.SubmitTransaction(transaction.ToUncommittedTransaction(), default);

        foreach (var i in Enumerable.Range(0, 10))
            await store.Append(i.ToString(), 1, NewEvent(), default);

        await subscription.StopAsync(default);

        handler.Verify();

        handler.Verify(s =>
                s.Handle(It.Is<EventEnvelope>(e => e.Timestamp != default)),
            Times.Exactly(130));

        list.Should().HaveCount(130);

        list.GroupBy(e => e.StreamId).Should().HaveCount(20);
        Assert.All(list.GroupBy(e => e.StreamId), g =>
        {
            var i = int.Parse(g.Key);
            g.Should().HaveCount(i < 100 ? 3 : 10);

            g.Should().BeInAscendingOrder(e => e.StreamPosition);
            Assert.All(g, e => e.Timestamp.Should().BeOnOrAfter(start).And.BeBefore(DateTime.UtcNow));
        });
    }
}