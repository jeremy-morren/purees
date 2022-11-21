using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PureES.Core.EventStore.Serialization;
using PureES.Core.ExpBuilders.Services;
using PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;
using PureES.Core.Tests.Models;
using PureES.EventStore.InMemory;
using PureES.EventStore.InMemory.Serialization;
using Xunit;

// ReSharper disable UnusedMember.Local
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
// ReSharper disable NotAccessedPositionalProperty.Local
// ReSharper disable UnusedType.Local

namespace PureES.Core.Tests.ServiceTests;

public class BuildCommandHandlerTests
{
    [Theory]
    [InlineData(typeof(Aggregate))]
    [InlineData(typeof(AggregateAsync))]
    [InlineData(typeof(AggregateValueTaskAsync))]
    public async Task BuildNoResult(Type aggregateType)
    {
        using var sp = BuildServices(aggregateType, out var create, out var update);

        var createHandler = sp.GetRequiredService<ICommandHandler<Commands.Create>>();
        var updateHandler = sp.GetRequiredService<ICommandHandler<Commands.Update>>();

        Assert.Equal((ulong) 0, await createHandler.Handle(create, default));

        await Verify(aggregateType, sp, create, null);

        Assert.Equal((ulong) 1, await updateHandler.Handle(update, default));

        await Verify(aggregateType, sp, create, update);
    }

    [Theory]
    [InlineData(typeof(AggregateResult))]
    [InlineData(typeof(AggregateAsyncResult))]
    [InlineData(typeof(AggregateValueTaskAsyncResult))]
    public async Task BuildResult(Type aggregateType)
    {
        using var sp = BuildServices(aggregateType, out var create, out var update);

        var createHandler = sp.GetRequiredService<ICommandHandler<Commands.Create>>();
        var updateHandler = sp.GetRequiredService<ICommandHandler<Commands.Update>>();

        Assert.Equal(create.Id, (await createHandler.Handle<Result>(create, default)).Id);

        await Verify(aggregateType, sp, create, null);

        Assert.Equal(create.Id, (await updateHandler.Handle<Result>(update, default)).Id);

        await Verify(aggregateType, sp, create, update);
    }

    private static ServiceProvider BuildServices(Type aggregateType, out Commands.Create create,
        out Commands.Update update)
    {
        create = Commands.Create.New();
        update = Commands.Update.New(create.Id);

        var createCmd = create;

        var updateCmd = update;
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(NullLoggerProvider.Instance);
        });

        services
            .AddPureESCore()
            .AddEventStoreSerializerCore(configureTypeMap: mapper =>
                mapper.AddAssembly(typeof(BuildCommandHandlerTests).Assembly))
            .AddInMemoryEventStoreSerializer<Metadata>()
            .AddInMemoryEventStore()
            .AddEventEnricher((c, _) =>
            {
                switch (c)
                {
                    case Commands.Create:
                        Assert.Equal(createCmd, c);
                        break;
                    case Commands.Update:
                        Assert.Equal(updateCmd, c);
                        break;
                    default:
                        throw new Exception($"Unknown command {c.GetType()}");
                }

                return new Metadata();
            })
            .AddOptimisticConcurrency(c =>
            {
                switch (c)
                {
                    case Commands.Update:
                        Assert.Equal(updateCmd, c);
                        return 0;
                    default:
                        throw new Exception($"Unknown command {c.GetType()}");
                }
            });

        services.AddSingleton(PureESServices.Build(aggregateType, new CommandHandlerBuilderOptions()));

        return services.BuildServiceProvider();
    }

    private static Task Verify(Type aggregateType,
        IServiceProvider services,
        Commands.Create create,
        Commands.Update? update)
    {
        var genericMethod = typeof(BuildCommandHandlerTests)
                                .GetMethod(nameof(VerifyGeneric), BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Exception("Unable to get generic verify method");
        genericMethod = genericMethod.MakeGenericMethod(aggregateType);
        return (Task) genericMethod.Invoke(null, new object?[]
        {
            services,
            create,
            update
        })!;
    }

    private static async Task VerifyGeneric<TAggregate>(IServiceProvider services,
        Commands.Create create,
        Commands.Update? update)
        where TAggregate : notnull
    {
        var revision = update != null ? (ulong) 1 : 0;
        var store = services.GetRequiredService<IAggregateStore<TAggregate>>();
        var aggregate = await store.Load(create.Id.StreamId, revision, default);
        Assert.Equal(revision + 1, aggregate.Version);

        TValue? GetProperty<TValue>()
        {
            var prop = typeof(TAggregate)
                           .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                           .SingleOrDefault(p => p.PropertyType == typeof(TValue))
                       ?? throw new Exception($"Unable to get property of type {typeof(TValue)}");
            return (TValue?) prop.GetValue(aggregate.Aggregate);
        }

        var created = GetProperty<EventEnvelope<Events.Created, Metadata>>();
        var updated = GetProperty<EventEnvelope<Events.Updated, Metadata>>();

        Assert.NotNull(created);
        Assert.Equal(create.Value, created?.Event.Value);

        if (update == null) return;
        Assert.NotNull(updated);
        Assert.Equal(update.Value, updated?.Event.Value);
    }

    private record Metadata;

    private record Result(TestAggregateId Id);


    #region Aggregates

    [Aggregate]
    private record Aggregate(EventEnvelope<Events.Created, Metadata> Created,
        EventEnvelope<Events.Updated, Metadata>? Updated)
    {
        public static Aggregate When(EventEnvelope<Events.Created, Metadata> e) => new(e, null);

        public static Aggregate When(Aggregate current, EventEnvelope<Events.Updated, Metadata> e)
            => current with {Updated = e};

        public static Events.Created Create([Command] Commands.Create cmd) => new(cmd.Id, cmd.Value);

        public static Events.Updated UpdateOn(Aggregate current, [Command] Commands.Update cmd)
        {
            Assert.NotNull(current);
            Assert.NotNull(current.Created);
            return new Events.Updated(cmd.Id, cmd.Value);
        }
    }

    [Aggregate]
    private record AggregateAsync(EventEnvelope<Events.Created, Metadata> Created,
        EventEnvelope<Events.Updated, Metadata>? Updated)
    {
        public static AggregateAsync When(EventEnvelope<Events.Created, Metadata> e) => new(e, null);

        public static AggregateAsync When(AggregateAsync current, EventEnvelope<Events.Updated, Metadata> e)
            => current with {Updated = e};

        public static Task<Events.Created> CreateAsync([Command] Commands.Create cmd)
            => Task.FromResult(new Events.Created(cmd.Id, cmd.Value));

        public static Task<Events.Updated> UpdateOnAsync(AggregateAsync current, [Command] Commands.Update cmd)
        {
            Assert.NotNull(current);
            Assert.NotNull(current.Created);
            return Task.FromResult(new Events.Updated(cmd.Id, cmd.Value));
        }
    }

    [Aggregate]
    private record AggregateValueTaskAsync(EventEnvelope<Events.Created, Metadata> Created,
        EventEnvelope<Events.Updated, Metadata>? Updated)
    {
        public static AggregateValueTaskAsync When(EventEnvelope<Events.Created, Metadata> e) => new(e, null);

        public static AggregateValueTaskAsync When(AggregateValueTaskAsync current,
            EventEnvelope<Events.Updated, Metadata> e)
            => current with {Updated = e};

        public static ValueTask<Events.Created> CreateAsync([Command] Commands.Create cmd)
            => ValueTask.FromResult(new Events.Created(cmd.Id, cmd.Value));

        public static ValueTask<Events.Updated> UpdateOnAsync(AggregateValueTaskAsync current,
            [Command] Commands.Update cmd)
        {
            Assert.NotNull(current);
            Assert.NotNull(current.Created);
            return ValueTask.FromResult(new Events.Updated(cmd.Id, cmd.Value));
        }
    }

    [Aggregate]
    private record AggregateResult(EventEnvelope<Events.Created, Metadata> Created,
        EventEnvelope<Events.Updated, Metadata>? Updated)
    {
        public static AggregateResult When(EventEnvelope<Events.Created, Metadata> e) => new(e, null);

        public static AggregateResult When(AggregateResult current, EventEnvelope<Events.Updated, Metadata> e)
            => current with {Updated = e};

        public static CommandResult<Events.Created, Result> CreateWithResult([Command] Commands.Create cmd)
            => new(new Events.Created(cmd.Id, cmd.Value), new Result(cmd.Id));

        public static CommandResult<Events.Updated, Result> UpdateWithResult(AggregateResult current,
            [Command] Commands.Update cmd)
        {
            Assert.NotNull(current);
            Assert.NotNull(current.Created);
            return new CommandResult<Events.Updated, Result>(new Events.Updated(cmd.Id, cmd.Value), new Result(cmd.Id));
        }
    }

    [Aggregate]
    private record AggregateAsyncResult(EventEnvelope<Events.Created, Metadata> Created,
        EventEnvelope<Events.Updated, Metadata>? Updated)
    {
        public static AggregateAsyncResult When(EventEnvelope<Events.Created, Metadata> e) => new(e, null);

        public static AggregateAsyncResult When(AggregateAsyncResult current, EventEnvelope<Events.Updated, Metadata> e)
            => current with {Updated = e};

        public static Task<CommandResult<Events.Created, Result>> CreateAsyncResult([Command] Commands.Create cmd)
        {
            var result = new CommandResult<Events.Created, Result>(
                new Events.Created(cmd.Id, cmd.Value),
                new Result(cmd.Id));
            return Task.FromResult(result);
        }

        public static Task<CommandResult<Events.Updated, Result>> UpdateAsyncResult(AggregateAsyncResult current,
            [Command] Commands.Update cmd)
        {
            Assert.NotNull(current);
            Assert.NotNull(current.Created);
            var result = new CommandResult<Events.Updated, Result>(
                new Events.Updated(cmd.Id, cmd.Value),
                new Result(cmd.Id));
            return Task.FromResult(result);
        }
    }


    [Aggregate]
    private record AggregateValueTaskAsyncResult(EventEnvelope<Events.Created, Metadata> Created,
        EventEnvelope<Events.Updated, Metadata>? Updated)
    {
        public static AggregateValueTaskAsyncResult When(EventEnvelope<Events.Created, Metadata> e) => new(e, null);

        public static AggregateValueTaskAsyncResult When(AggregateValueTaskAsyncResult current,
            EventEnvelope<Events.Updated, Metadata> e)
            => current with {Updated = e};

        public static ValueTask<CommandResult<Events.Created, Result>> CreateAsyncResult([Command] Commands.Create cmd)
        {
            var result = new CommandResult<Events.Created, Result>(
                new Events.Created(cmd.Id, cmd.Value),
                new Result(cmd.Id));
            return ValueTask.FromResult(result);
        }

        public static ValueTask<CommandResult<Events.Updated, Result>> UpdateAsyncResult(
            AggregateValueTaskAsyncResult current,
            [Command] Commands.Update cmd)
        {
            Assert.NotNull(current);
            Assert.NotNull(current.Created);
            var result = new CommandResult<Events.Updated, Result>(
                new Events.Updated(cmd.Id, cmd.Value),
                new Result(cmd.Id));
            return ValueTask.FromResult(result);
        }
    }

    #endregion
}