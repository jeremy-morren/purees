﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Shouldly;

// ReSharper disable PartialTypeWithSinglePart

namespace PureES.Tests.Models;

[Aggregate, PublicAPI]
public partial class TestAggregate
{
    public required EventEnvelope<Events.Created, Metadata> Created { get; init; }
    public Events.Updated? Updated { get; private set; }
    public uint StreamPosition { get; private set; }

    public static Events.Created CreateOn([Command] Commands.Create cmd) => new(cmd.Id, cmd.Value);

    public Task<Events.Updated[]> UpdateOn([Command] Commands.Update cmd, [FromServices] IServiceProvider service, CancellationToken ct)
    {
        Created.ShouldNotBeNull();
        service.ShouldNotBeNull();
        return Task.FromResult(new[] { new Events.Updated(cmd.Id, cmd.Value) });
    }
    
    
    public CommandResult<Events.Updated, int[]> UpdateOnResult([Command] Commands.UpdateConstantStream cmd, [FromServices] IServiceProvider service)
    {
        Created.ShouldNotBeNull();
        service.ShouldNotBeNull();
        return new CommandResult<Events.Updated, int[]>(
            new Events.Updated(Created.Event.Id, cmd.Value),
            new [] { cmd.Value });
    }
    
    public static IAsyncEnumerable<Events.Created> CreateOnAsyncEnumerable([Command] int[] cmd, [FromServices] ILoggerFactory lf)
    {
        lf.ShouldNotBeNull();
        return AsyncEnumerable.Empty<Events.Created>();
    }

    public static Events.Updated UpdateOnStatic([Command] decimal d, 
        TestAggregate current,
        [FromServices] IServiceProvider services)
    {
        current.ShouldNotBeNull();
        return new Events.Updated(current.Created.Event.Id, (int)d);
    }
    
    public static EventsTransaction CreateTransaction([Command] uint u) => throw new NotImplementedException();
    
    public EventsTransaction UpdateTransaction([Command] ushort u) => throw new NotImplementedException();
    
    public ValueTask<EventsTransaction> TransactionValueTask([Command] short u) => throw new NotImplementedException();

    public static IEventsTransaction TransactionInterface([Command] ISerializable s) =>
        throw new NotImplementedException();
    
    public static IEventsTransaction? TransactionInterfaceNull([Command] uint[] _) =>
        throw new NotImplementedException();

    public static DerivedTransaction DerivedTransaction([Command] ushort[] x) => throw new NotImplementedException();
    
    public Task<CommandResult<EventsTransaction, object>> TransactionCommandResult([Command] long[] u) => 
        throw new NotImplementedException();

    public void GlobalWhen(EventEnvelope envelope, CancellationToken ct)
    {
        StreamPosition = envelope.StreamPosition;
    }
    
    public Task GlobalWhenAsync(EventEnvelope envelope, [FromServices] ILoggerFactory lf)
    {
        lf.ShouldNotBeNull();
        StreamPosition = envelope.StreamPosition;
        return Task.CompletedTask;
    }

    public static TestAggregate When(EventEnvelope<Events.Created, Metadata> envelope)
    {
        return new TestAggregate()
        {
            Created = envelope
        };
    }

    public void When([Event] Events.Updated e, [FromServices] IServiceProvider service)
    {
        e.ShouldNotBeNull();
        service.ShouldNotBeNull();
        Updated = e;
    }
    
    public Task When(EventEnvelope<int> envelope, [FromServices] ILoggerFactory service)
    {
        envelope.ShouldNotBeNull();
        service.ShouldNotBeNull();
        StreamPosition.ShouldNotBe(envelope.StreamPosition);
        return Task.CompletedTask;
    }

    public static TestAggregate UpdateWhenStatic([Event] Events.Updated e, TestAggregate current) => current;
}

public class DerivedTransaction : EventsTransaction;