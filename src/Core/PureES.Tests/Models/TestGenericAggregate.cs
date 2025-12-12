using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace PureES.Tests.Models;

[PublicAPI]
public record TestGenericAggregate<TAggregate, TCommand, TEvent> 
    where TAggregate : TestGenericAggregate<TAggregate, TCommand, TEvent>
    where TEvent : new()
{
    public required TEvent Event { get; set; }

    public static TEvent Create([Command] TCommand command) => new();

    public TEvent Update([Command] Dictionary<string, TCommand> command) => new();

    public static TAggregate When([Event] TEvent e)
    {
        var a = (TAggregate)Activator.CreateInstance(typeof(TAggregate))!;
        a.Event = e;
        return a;
    }
}

[Aggregate]
public record ImplementedGenericAggregate : TestGenericAggregate<ImplementedGenericAggregate, object, object>
{
}