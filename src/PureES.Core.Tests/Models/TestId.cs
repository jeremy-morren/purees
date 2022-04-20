using System;

namespace PureES.Core.Tests.Models;

public record TestAggregateId(Guid Id)
{
    public string StreamId => $"TestAggregates-{Id}";

    public static TestAggregateId New() => new(Guid.NewGuid());
}