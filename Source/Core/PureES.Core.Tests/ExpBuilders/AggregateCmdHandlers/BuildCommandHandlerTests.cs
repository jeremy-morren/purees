using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders.AggregateCmdHandlers;
using PureES.Core.Tests.Models;
using PureES.EventStore.InMemory;
using PureES.EventStoreDB.Serialization;
using Xunit;

// ReSharper disable UnusedMember.Local
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
// ReSharper disable NotAccessedPositionalProperty.Local
// ReSharper disable UnusedType.Local

namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;

public class BuildCommandHandlerTests
{
    [Theory]
    [InlineData(typeof(Aggregate))]
    [InlineData(typeof(AggregateAsync))]
    public async Task BuildNoResult(Type aggregateType)
    {
        await using var sp = BuildServices(aggregateType, out var create, out var update);

        var createHandler = sp.GetRequiredService<CommandHandler<Commands.Create>>();
        var updateHandler = sp.GetRequiredService<CommandHandler<Commands.Update>>();

        Assert.Equal((ulong) 0, await createHandler(create, default));

        await Verify(aggregateType, sp, create, null);
        
        Assert.Equal((ulong) 1, await updateHandler(update, default));

        await Verify(aggregateType, sp, create, update);
    }
    
    [Theory]
    [InlineData(typeof(AggregateResult))]
    [InlineData(typeof(AggregateAsyncResult))]
    public async Task BuildResult(Type aggregateType)
    {
        await using var sp = BuildServices(aggregateType, out var create, out var update);

        var createHandler = sp.GetRequiredService<CommandHandler<Commands.Create, Result>>();
        var updateHandler = sp.GetRequiredService<CommandHandler<Commands.Update, Result>>();

        Assert.Equal(create.Id, (await createHandler(create, default)).Id);

        await Verify(aggregateType, sp, create, null);
        
        Assert.Equal(create.Id, (await updateHandler(update, default)).Id);

        await Verify(aggregateType, sp, create, update);
    }

    private static ServiceProvider BuildServices(Type aggregateType, out Commands.Create create, out Commands.Update update)
    {
        create = Commands.Create.New();
        update = Commands.Update.New(create.Id);

        var createCmd = create;
        var updateCmd = update;
        
        return Services.Build(services =>
        {
            services.AddInMemoryEventStore()
                .AddEventStoreDBSerializer<Metadata>(configureTypeMapper: mapper => mapper.AddAssembly(typeof(BuildCommandHandlerTests).Assembly))
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
            new CommandHandlerBuilder(new CommandHandlerOptions())
                .AddCommandServices(services, aggregateType);
        });
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
        return (Task)genericMethod.Invoke(null, new object?[]
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
        var version = update != null ? (ulong)1 : 0;
        var store = services.GetRequiredService<IAggregateStore<TAggregate>>();
        var aggregate = await store.Load(create.Id.StreamId, version, default);
        Assert.Equal(version, aggregate.Revision);

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

    [Aggregate]
    private record Aggregate(EventEnvelope<Events.Created, Metadata> Created,
        EventEnvelope<Events.Updated, Metadata>? Updated)
    {
        public static Aggregate When(EventEnvelope<Events.Created, Metadata> e) => new(e, null);

        public static Aggregate When(Aggregate current, EventEnvelope<Events.Updated, Metadata> e)
            => current with {Updated = e};
        
        public static Events.Created Create([Command] Commands.Create cmd) => new (cmd.Id, cmd.Value);

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
    private record AggregateResult(EventEnvelope<Events.Created, Metadata> Created,
        EventEnvelope<Events.Updated, Metadata>? Updated)
    {
        public static AggregateResult When(EventEnvelope<Events.Created, Metadata> e) => new(e, null);

        public static AggregateResult When(AggregateResult current, EventEnvelope<Events.Updated, Metadata> e)
            => current with {Updated = e};
        
        public static CommandResult<Events.Created, Result> CreateWithResult([Command] Commands.Create cmd)
            => new (new Events.Created(cmd.Id, cmd.Value), new Result(cmd.Id));
        
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

        public static Task<CommandResult<Events.Created, Result>> CreateWithResultAsync([Command] Commands.Create cmd)
        {
            var result = new CommandResult<Events.Created, Result>(
                new Events.Created(cmd.Id, cmd.Value), 
                new Result(cmd.Id));
            return Task.FromResult(result);
        }

        public static Task<CommandResult<Events.Updated, Result>> UpdateWithResultAsync(AggregateAsyncResult current, 
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
}