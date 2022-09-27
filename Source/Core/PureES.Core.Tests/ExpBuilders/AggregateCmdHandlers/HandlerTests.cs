// using System;
// using System.Collections.Generic;
// using System.Collections.Immutable;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.DependencyInjection;
// using PureES.Core.ExpBuilders.AggregateCmdHandlers;
// using PureES.Core.Tests.Models;
// using Xunit;
// // ReSharper disable MemberCanBePrivate.Local
//
// namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;
//
// public class HandlerTests
// {
//     [Fact]
//     public void Add_Handlers()
//     {
//         using var sp = Services.Build(services =>
//         {
//             services.AddSingleton<IEventStore, FakeEventStore>();
//             new CommandHandlerBuilder(new CommandHandlerOptions())
//                 .AddCommandServices(services, typeof(Aggregate));
//         });
//         Assert.NotNull(sp.GetService<CommandHandler<Commands.Update>>());
//         Assert.NotNull(sp.GetService<CommandHandler<Commands.Create>>());
//         Assert.NotNull(sp.GetService<Func<ImmutableArray<EventEnvelope>, Aggregate>>());
//     }
//     
//     [Fact]
//     public async Task Create_Update()
//     {
//         await using var sp = Services.Build(services =>
//         {
//             services.AddSingleton<IEventStore, FakeEventStore>()
//                 .AddSingleton(Service.Instance);
//             new CommandHandlerBuilder(new CommandHandlerOptions()
//                 {
//                     GetMetadata = (_,_) => new Metadata()
//                 })
//                 .AddCommandServices(services, typeof(Aggregate));
//         });
//
//         var create = sp.GetRequiredService<CommandHandler<Commands.Create>>();
//         var update = sp.GetRequiredService<CommandHandler<Commands.Update>>();
//         var id = TestAggregateId.New();
//
//         Aggregate Load()
//         {
//             var store = sp!.GetRequiredService<IEventStore>();
//             var events = store.Load(id!.StreamId, null, default).Result;
//             return sp!.GetRequiredService<Func<ImmutableArray<EventEnvelope>, Aggregate>>()(events);
//         }
//
//         Assert.Equal((ulong)0, await create(new Commands.Create(id, Rand.NextInt())));
//         Assert.Equal((ulong)1, await update(new Commands.Update(id, Rand.NextInt())));
//         Assert.Equal((ulong)2, await update(new Commands.Update(id, Rand.NextInt())));
//
//         Assert.NotEmpty(Load().Events);
//         Assert.Equal(3, Load().Events.Length);
//     }
//     
//     [Aggregate]
//     private record Aggregate(ImmutableArray<object> Events)
//     {
//         public static Aggregate When(EventEnvelope<Events.Created, Metadata> @event) =>
//             new(ImmutableArray.Create<object>(@event));
//
//         public static Aggregate When(Aggregate current, EventEnvelope<Events.Updated, Metadata> @event) =>
//             current with {Events = current.Events.Add(@event)};
//
//         public static Events.Created CreateOn([Command] Commands.Create cmd,
//             [FromServices] Service svc)
//         {
//             Assert.Same(Service.Instance, svc);
//             return new Events.Created(cmd.Id, cmd.Value);
//         }
//
//         public static Events.Updated UpdateOn(Aggregate aggregate, 
//             [Command] Commands.Update cmd,
//             [FromServices] Service svc)
//         {
//             Assert.Same(Service.Instance, svc);
//             Assert.NotNull(aggregate);
//             Assert.NotEmpty(aggregate.Events);
//             return new Events.Updated(cmd.Id, cmd.Value);
//         }
//
//         public static Task<Events.Updated> UpdateOnAsync(Aggregate aggregate,
//             [Command] Commands.Update cmd,
//             [FromServices] Service svc) => Task.FromResult(UpdateOn(aggregate, cmd, svc));
//     }
//
//     private record Metadata;
//
//     public class Service
//     {
//         private Service() {}
//
//         public static readonly Service Instance = new Service();
//     }
//     
//     private class FakeEventStore : IEventStore
//     {
//         private readonly Dictionary<string, ImmutableArray<EventEnvelope>> _events = new ();
//
//         public Task<ulong> Create(string streamId, UncommittedEvent @event, CancellationToken _)
//         {
//             if (_events.ContainsKey(streamId))
//                 throw new ArgumentException("Stream already exists");
//             var envelope = new EventEnvelope(@event.EventId,
//                 streamId,
//                 0L,
//                 DateTime.UtcNow,
//                 @event.Event,
//                 @event.Metadata);
//             _events.Add(streamId, ImmutableArray.Create(envelope));
//             return Task.FromResult(envelope.StreamPosition);
//         }
//
//         public Task<ulong> Append(string streamId, ulong expectedVersion, UncommittedEvent @event, CancellationToken _)
//         {
//             if (!_events.TryGetValue(streamId, out var events))
//                 throw new ArgumentException("Stream does not exist");
//             if ((int)expectedVersion != events.Length)
//                 throw new ArgumentException(
//                     $"Wrong stream version. Expected: {expectedVersion}, Actual: {events.Length}");
//             var envelope = new EventEnvelope(@event.EventId,
//                 streamId,
//                 (ulong)events.Length,
//                 DateTime.UtcNow,
//                 @event.Event,
//                 @event.Metadata);
//             _events[streamId] = events.Add(envelope);
//             return Task.FromResult(envelope.StreamPosition);
//         }
//
//         public Task<ImmutableArray<EventEnvelope>> Load(string streamId, ulong? expectedVersion, CancellationToken cancellationToken)
//         {
//             if (!_events.TryGetValue(streamId, out var events))
//                 throw new ArgumentException("Stream does not exist");
//             if (expectedVersion != null && (int) expectedVersion.Value != events.Length)
//                 throw new ArgumentException(
//                     $"Wrong stream version. Expected: {expectedVersion}, Actual: {events.Length}");
//             return Task.FromResult(events);
//         }
//         
//         public Task<ulong> Create(string streamId, 
//             ImmutableArray<UncommittedEvent> events, 
//             CancellationToken cancellationToken) =>
//             throw new System.NotImplementedException();
//
//         public Task<ulong> Append(string streamId, 
//             ulong expectedVersion, 
//             ImmutableArray<UncommittedEvent> events, 
//             CancellationToken cancellationToken) => 
//             throw new System.NotImplementedException();
//     }
// }